namespace Rmg.BuildingBlocks.Tenancy;

/// <summary>Per-request tenant/facility/user context, populated only from verified JWT claims (R-2).</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? FacilityId { get; }
    Guid? UserId { get; }
    void Set(Guid? tenantId, Guid? facilityId, Guid? userId);
    void Clear();
}
