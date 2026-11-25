using Microsoft.AspNetCore.Http;
using Rmg.BuildingBlocks.Common;

namespace Rmg.BuildingBlocks.Auth;

/// <summary>Writes the R-1 problem+json shape from pipeline stages that don't throw through the
/// exception handler (JwtBearer challenge/forbid, rate-limit rejection).</summary>
public static class ProblemJson
{
    public static Task WriteAsync(HttpContext http, int status, string code, string title, string? detail = null)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "about:blank",
                ["title"] = title,
                ["status"] = status,
                ["detail"] = detail,
                ["code"] = code,
                ["errors"] = null,
                ["correlationId"] = http.Items[CorrelationMiddleware.CorrelationIdItemKey],
            },
            options: null,
            contentType: "application/problem+json");
    }
}
