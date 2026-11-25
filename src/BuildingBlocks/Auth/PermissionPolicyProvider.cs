using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Rmg.BuildingBlocks.Auth;

/// <summary>Builds "perm:&lt;code&gt;" policies on demand; everything else falls through to the defaults.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public static string PolicyFor(string code) => $"{Prefix}{code}";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
                .Build();

        return await base.GetPolicyAsync(policyName);
    }
}
