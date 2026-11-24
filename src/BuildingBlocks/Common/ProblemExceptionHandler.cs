using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Rmg.BuildingBlocks.Common;

// R-1: every error is RFC 9457 problem+json with a stable code + correlationId; never a stack trace.
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception as RmgProblemException
            ?? new RmgProblemException("INTERNAL_ERROR", "An unexpected error occurred.", 500);

        httpContext.Response.StatusCode = problem.Status;

        if (problem.Status == 500)
        {
            logger.LogError(exception, "Unhandled exception (correlationId {CorrelationId})",
                httpContext.Items[CorrelationMiddleware.CorrelationIdItemKey]);
        }

        await httpContext.Response.WriteAsJsonAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "about:blank",
                ["title"] = problem.Title,
                ["status"] = problem.Status,
                ["detail"] = problem.Detail,
                ["code"] = problem.Code,
                ["errors"] = problem.Errors,
                ["correlationId"] = httpContext.Items[CorrelationMiddleware.CorrelationIdItemKey],
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
