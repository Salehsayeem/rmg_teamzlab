#if DEBUG
// THROWAWAY Task 4 probes exercising [RequirePerm] authorization and claim → ITenantContext resolution.
// Companion to DevProbeEndpoints — deleted by a later task, do not build features on these.
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Api;

public static class AuthProbeEndpoints
{
    public static void MapAuthProbeEndpoints(this WebApplication app)
    {
        app.MapGet("/authz/probe/perm", PermProbe);
        app.MapGet("/authz/probe/tenant",
            (ITenantContext tenant) => Envelope<object>.Of(new { tenant.TenantId, tenant.FacilityId, tenant.UserId }));

        [RequirePerm("pdm.style.create")]
        static object PermProbe() => Envelope<string>.Of("ok");
    }
}
#endif
