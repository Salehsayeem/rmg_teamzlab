using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.Merchandising.Domain;

// ord.order_line — one style of a customer order. style_id + style_version_id snapshot the pinned
// immutable revision (capture requires an Approved version); both point at pdm through raw-SQL
// composite FKs (ruling W2-b: no EF navigation across modules). LineQty is derived = Σ size
// quantities, never client-supplied; price numeric(18,2) ≥ 0 and line_qty numeric(18,0) are CHECKed.
public class OrderLine : TenantEntity
{
    public Guid OrderId { get; set; }
    public int LineNo { get; set; }
    public Guid StyleId { get; set; }
    public Guid StyleVersionId { get; set; }
    public decimal Price { get; set; }
    public decimal LineQty { get; set; }
}
