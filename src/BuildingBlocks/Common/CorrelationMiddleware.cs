using Microsoft.AspNetCore.Http;

namespace Rmg.BuildingBlocks.Common;

// R-1: every request gets a correlation id — inbound X-Correlation-Id echoed, otherwise a fresh Guid.
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string CorrelationIdItemKey = "correlationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : Guid.NewGuid().ToString();

        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        await next(context);
    }
}
