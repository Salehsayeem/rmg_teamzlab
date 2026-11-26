using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.Merchandising.Domain;

// ord.size_quantity — one ordered (color, size) cell of a line; qty numeric(18,0) > 0 (service 422
// QTY_POSITIVE + DB CHECK), unique per (tenant_id, order_line_id, color_code, size_code).
public class SizeQuantity : TenantEntity
{
    public Guid OrderLineId { get; set; }
    public string ColorCode { get; set; } = null!;
    public string SizeCode { get; set; } = null!;
    public decimal Qty { get; set; }
}
