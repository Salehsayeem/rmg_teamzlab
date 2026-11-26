using Microsoft.AspNetCore.Authorization;

namespace Rmg.BuildingBlocks.Auth;

public sealed record PermissionRequirement(string Code) : IAuthorizationRequirement;

// Succeeds iff the token carries the required permission code; otherwise the authorization
// middleware turns the failure into a 403 problem+json (JwtBearer OnForbidden) — never a leak.
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(c => c.Type == "perm" && c.Value == requirement.Code))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
