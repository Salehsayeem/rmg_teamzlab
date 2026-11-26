using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.Merchandising.Domain;

// ord.buyer — baseline columns inherited from TenantEntity (ruling P-10).
public class Buyer : TenantEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    // ISO 3166 alpha-2 / ISO 4217 alpha-3 — char(2)/char(3) columns (lengths validated at the API).
    public string Country { get; set; } = null!;
    public string Currency { get; set; } = null!;

    // Server-set lifecycle ('Active' on create); client-provided status is never authoritative.
    public string Status { get; set; } = "Active";
}
