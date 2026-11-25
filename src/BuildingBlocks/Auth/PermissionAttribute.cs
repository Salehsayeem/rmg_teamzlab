using Microsoft.AspNetCore.Authorization;

namespace Rmg.BuildingBlocks.Auth;

/// <summary>Protects an endpoint with a single permission code, e.g. [RequirePerm("pdm.style.create")].
/// Maps to a "perm:&lt;code&gt;" policy resolved by PermissionPolicyProvider.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePermAttribute(string code)
    : AuthorizeAttribute(PermissionPolicyProvider.PolicyFor(code));
