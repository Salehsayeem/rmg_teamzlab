using Rmg.Modules.Organization.Contracts;

namespace Rmg.Modules.Organization.Application;

public interface IFacilityRepository
{
    Task<FacilityDto[]> ListAsync(CancellationToken ct = default);
    Task<FacilityDto?> FindAsync(Guid id, CancellationToken ct = default);
}

// Facility reads: pass-through today (scoping + projection live in the repository); the seam where
// facility business rules attach in later tasks (Controller → Service → Repository layering).
public sealed class FacilityService(IFacilityRepository facilities) : IOrganizationApi
{
    public Task<FacilityDto[]> ListFacilitiesAsync(CancellationToken ct = default) => facilities.ListAsync(ct);
    public Task<FacilityDto?> FindFacilityAsync(Guid id, CancellationToken ct = default) => facilities.FindAsync(id, ct);
}
