using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.Organization.Domain;

// org.facility — baseline columns inherited from TenantEntity (ruling P-10); the mapped model is unchanged.
public class Facility : TenantEntity
{
    public Guid LegalEntityId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Timezone { get; set; } = null!;
    public string? Address { get; set; }
}
