using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.Merchandising.Domain;

// ord.delivery_schedule — one shipment slot of a line: qty numeric(18,0) > 0 (DB CHECK).
public class DeliverySchedule : TenantEntity
{
    public Guid OrderLineId { get; set; }
    public DateOnly ShipDate { get; set; }
    public decimal Qty { get; set; }
}
