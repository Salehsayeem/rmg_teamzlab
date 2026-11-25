using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.BuildingBlocks.Idempotency;

/// <summary>Endpoint filter backing [Idempotent]: a POST carrying Idempotency-Key is looked up by
/// (tenant, endpoint, sha256(key)). Hit with a matching request hash → the stored response is replayed
/// verbatim with the Idempotent-Replay: true header; hit with a mismatched hash → 422
/// IDEMPOTENCY_KEY_REUSED; miss → the endpoint executes and its response is stored, 2xx only.
/// No header, non-POST, anonymous, or unmarked endpoints pass straight through.</summary>
public sealed class IdempotencyFilter : IEndpointFilter
{
    public const string ReplayHeaderName = "Idempotent-Replay";

    /// <summary>HttpContext.Items key under which the Api pipeline guard middleware hands the filter the
    /// raw request body it buffered (see Program.cs).</summary>
    public const string RequestBodyItemKey = "idempotency:requestBody";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!HttpMethods.IsPost(http.Request.Method)
            || http.GetEndpoint()?.Metadata.GetMetadata<IdempotentAttribute>() is null)
            return await next(context);

        var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var tenantId = http.RequestServices.GetRequiredService<ITenantContext>().TenantId;
        if (string.IsNullOrWhiteSpace(key) || tenantId is null)
            return await next(context);

        // Route template (falling back to the path) so templated endpoints share one record per template.
        var endpoint = http.GetEndpoint() is RouteEndpoint routed
            ? routed.RoutePattern.RawText ?? http.Request.Path.Value!
            : http.Request.Path.Value!;
        // The request body is captured by the Api pipeline guard middleware (the minimal-API pipeline
        // drains the body before filters run) and hashed to detect key reuse with a different payload.
        var requestHash = Sha256(http.Items.TryGetValue(RequestBodyItemKey, out var body) ? (string)body! : string.Empty);
        var keyHash = Sha256(key);

        var db = http.RequestServices.GetRequiredService<DbContext>();
        var records = db.Set<IdempotencyRecord>();
        var stored = await records.FirstOrDefaultAsync(r =>
            r.TenantId == tenantId && r.Endpoint == endpoint && r.KeyHash == keyHash);
        if (stored is not null)
        {
            if (stored.RequestHash != requestHash)
                throw new BusinessRuleProblem("IDEMPOTENCY_KEY_REUSED",
                    "This Idempotency-Key was already used with a different request body.");
            return new ReplayResult(stored.ResponseStatus, stored.ResponseJson);
        }

        // Capture the endpoint's response bytes so a 2xx can be stored, then re-emit them untouched.
        // Minimal-API results execute AFTER the filter chain returns, so the filter must execute the
        // IResult itself to see the body; non-IResult returns pass through uncaptured (never stored).
        var original = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        object? result;
        try
        {
            result = await next(context);
            if (result is IResult executable)
            {
                await executable.ExecuteAsync(http);
                // The pipeline always executes the outermost filter's return value — hand it a no-op
                // result (null would get JSON-serialized against the already-started response).
                result = Results.Empty;
            }
        }
        finally
        {
            http.Response.Body = original;
        }

        if (http.Response.StatusCode is >= 200 and < 300 && buffer.Length > 0)
        {
            records.Add(new IdempotencyRecord
            {
                TenantId = tenantId.Value,
                Endpoint = endpoint,
                KeyHash = keyHash,
                RequestHash = requestHash,
                ResponseStatus = http.Response.StatusCode,
                ResponseJson = Encoding.UTF8.GetString(buffer.ToArray()),
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        if (buffer.Length > 0)
            await original.WriteAsync(buffer.GetBuffer(), 0, (int)buffer.Length);
        return result;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Replays a stored response verbatim, flagged with Idempotent-Replay: true.</summary>
    private sealed class ReplayResult(int status, string json) : IResult
    {
        public async Task ExecuteAsync(HttpContext http)
        {
            http.Response.StatusCode = status;
            http.Response.ContentType = "application/json";
            http.Response.Headers[ReplayHeaderName] = "true";
            await http.Response.WriteAsync(json);
        }
    }
}
