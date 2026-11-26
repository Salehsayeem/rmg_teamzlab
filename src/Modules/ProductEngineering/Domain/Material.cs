using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.ProductEngineering.Domain;

// pdm.material — tenant-owned master data on the shared TenantEntity baseline (P-10). BaseUomCode
// references the global pdm.uom catalogue; (tenant_id, code) is unique per tenant.
public class Material : TenantEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Category { get; set; }
    public string BaseUomCode { get; set; } = null!;
}
