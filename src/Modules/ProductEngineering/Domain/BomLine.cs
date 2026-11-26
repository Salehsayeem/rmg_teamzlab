using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.ProductEngineering.Domain;

// pdm.bom_line — material consumption of a BOM: qty per unit numeric(18,4) > 0 (service 422
// QTY_POSITIVE + DB CHECK), waste_pct numeric(5,2), uom references the global pdm.uom catalogue.
public class BomLine : TenantEntity
{
    public Guid BomId { get; set; }
    public Guid MaterialId { get; set; }
    public decimal QtyPerUnit { get; set; }
    public string UomCode { get; set; } = null!;
    public decimal WastePct { get; set; }
}
