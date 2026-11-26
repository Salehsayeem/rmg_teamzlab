using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.BuildingBlocks.Auth;

/// <summary>Runs after UseAuthentication: verified token claims tid/fid/sub become the request's
/// ITenantContext. An authenticated token without tid is rejected 401 UNAUTHENTICATED; anonymous
/// requests pass through so the authorization middleware can challenge them itself.</summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = Parse(context.User.FindFirstValue("tid"));
            if (tenantId is null)
                throw new UnauthorizedProblem("Token carries no tenant.");

            tenant.Set(tenantId, Parse(context.User.FindFirstValue("fid")), Parse(context.User.FindFirstValue("sub")));
        }

        await next(context);
    }

    private static Guid? Parse(string? value) => Guid.TryParse(value, out var id) ? id : null;
}
