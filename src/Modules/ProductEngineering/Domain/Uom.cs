namespace Rmg.Modules.ProductEngineering.Domain;

// pdm.uom — global reference data (code is the key). Deliberately NOT a TenantEntity: UOMs are
// shared across tenants, so no tenant_id column and no query filter. Seeded by 003_PdmMaster.
public class Uom
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
}
