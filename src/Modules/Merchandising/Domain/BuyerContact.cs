using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.Merchandising.Domain;

// ord.buyer_contact — a buyer's contact person. Email/phone are purpose-limited fields: they leave
// the API only for holders of ord.buyer.manage (property-level authz, BuyersController).
public class BuyerContact : TenantEntity
{
    public Guid BuyerId { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
