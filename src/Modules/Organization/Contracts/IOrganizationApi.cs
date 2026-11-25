namespace Rmg.Modules.Organization.Contracts;

/// <summary>The ORG module's API surface as consumed by its controllers (Controller → Service layering).</summary>
public interface IOrganizationApi
{
    Task<FacilityDto[]> ListFacilitiesAsync(CancellationToken ct = default);
    Task<FacilityDto?> FindFacilityAsync(Guid id, CancellationToken ct = default);
}
