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
/// No header, non-POST, anonymous, or unmarked endpoints pass straight through.
///
/// Claim-first: a concurrent racer cannot double-execute the write — the owner inserts a Pending claim
/// (committed before the endpoint runs) and the racer's insert violates the (tenant, endpoint, key_hash)
/// key, so it is refused 409 IDEMPOTENCY_KEY_IN_FLIGHT before reaching the handler. A crash mid-flight
/// strands the claim as Pending (pilot: no TTL sweeper); non-2xx/failed executions release the claim so
/// a genuine retry can proceed.</summary>
public sealed class IdempotencyFilter : IEndpointFilter
{
    public const string ReplayHeaderName = "Idempotent-Replay";

    /// <summary>HttpContext.Items key under which the Api pipeline guard middleware hands the filter the
    /// raw request body it buffered (see Program.cs).</summary>
    public const string RequestBodyItemKey = "idempotency:requestBody";

    /// <summary>ResponseStatus of a claim whose owner is still executing; jsonb cannot store an empty
    /// string, so empty 2xx bodies and pending claims are both stored as JSON null (distinguished by
    /// status — replay only ever serves completed 2xx claims).</summary>
    private const int PendingStatus = 0;
    private const string NullJson = "null";
    private const string InFlightCode = "IDEMPOTENCY_KEY_IN_FLIGHT";

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
            if (!IsCompleted(stored))
                throw new RmgProblemException(InFlightCode, "Conflict.", 409,
                    Detail: "An identical request is already in flight.");
            return new ReplayResult(stored.ResponseStatus, stored.ResponseJson);
        }

        // Claim the key atomically BEFORE executing: the unique key turns a racing racer's insert into a
        // violation, so it is refused before it can run the write (no 500-after-commit lost updates).
        records.Add(new IdempotencyRecord
        {
            TenantId = tenantId.Value,
            Endpoint = endpoint,
            KeyHash = keyHash,
            RequestHash = requestHash,
            ResponseStatus = PendingStatus,
            ResponseJson = NullJson,
            CreatedAtUtc = DateTime.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new RmgProblemException(InFlightCode, "Conflict.", 409,
                Detail: "An identical request is already in flight.");
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
        catch
        {
            await ReleaseClaimAsync(db, tenantId.Value, endpoint, keyHash);
            throw;
        }
        finally
        {
            http.Response.Body = original;
        }

        if (http.Response.StatusCode is >= 200 and < 300)
        {
            var claim = await records.SingleAsync(r =>
                r.TenantId == tenantId && r.Endpoint == endpoint && r.KeyHash == keyHash);
            var responseBody = Encoding.UTF8.GetString(buffer.ToArray());
            claim.ResponseStatus = http.Response.StatusCode;
            claim.ResponseJson = responseBody.Length == 0 ? NullJson : responseBody;
            await db.SaveChangesAsync();
        }
        else
        {
            await ReleaseClaimAsync(db, tenantId.Value, endpoint, keyHash);
        }

        if (buffer.Length > 0)
            await original.WriteAsync(buffer.GetBuffer(), 0, (int)buffer.Length);
        return result;
    }

    private static bool IsCompleted(IdempotencyRecord record) => record.ResponseStatus is >= 200 and < 300;

    // Raw SQL: the cleanup path also runs after a failed endpoint SaveChanges, whose poisoned tracked
    // entries would make another SaveChangesAsync re-fail before it ever reached the claim delete.
    private static async Task ReleaseClaimAsync(DbContext db, Guid tenantId, string endpoint, string keyHash)
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM iam.idempotency_record WHERE tenant_id = {tenantId} AND endpoint = {endpoint} AND key_hash = {keyHash}");
        }
        catch
        {
            // The claim may be left Pending; the client must see the original failure regardless.
        }
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Replays a stored response verbatim, flagged with Idempotent-Replay: true. Empty bodies
    /// (stored as the jsonb NullJson placeholder) replay as a bodyless response with the same status.</summary>
    private sealed class ReplayResult(int status, string json) : IResult
    {
        public async Task ExecuteAsync(HttpContext http)
        {
            http.Response.StatusCode = status;
            http.Response.Headers[ReplayHeaderName] = "true";
            if (json.Length == 0 || json == NullJson)
                return;
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsync(json);
        }
    }
}
