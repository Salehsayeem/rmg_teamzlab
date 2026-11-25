namespace Rmg.BuildingBlocks.Tenancy;

/// <summary>Scoped implementation — ASP.NET Core's DI scope gives one instance per request,
/// which is the async-local guarantee the plan calls for. Ambient outside HTTP (workers/tests):
/// construct or Set() manually within the scope.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? FacilityId { get; private set; }
    public Guid? UserId { get; private set; }

    public void Set(Guid? tenantId, Guid? facilityId, Guid? userId)
    {
        TenantId = tenantId;
        FacilityId = facilityId;
        UserId = userId;
    }

    public void Clear() => Set(null, null, null);
}
