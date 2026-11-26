using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.Merchandising.Domain;

// ord.customer_order — baseline columns inherited from TenantEntity (ruling P-10). buyer_id points at
// ord.buyer through a raw-SQL composite FK (ruling W2-b: no EF navigation across modules); facility_id
// is the nullable baseline pointer (pilot JWTs carry no fid, so it stays null until claims issue it).
public class CustomerOrder : TenantEntity
{
    public Guid? FacilityId { get; set; }
    public string Code { get; set; } = null!;
    public Guid BuyerId { get; set; }
    public string BuyerPo { get; set; } = null!;

    // ISO 4217 alpha-3 — char(3) column (length validated at the API).
    public string Currency { get; set; } = null!;
    public DateOnly OrderDate { get; set; }

    // Server-set lifecycle ('Draft' on capture); client-provided status is never authoritative.
    public string Status { get; set; } = "Draft";

    // Write-side state machine: Draft→Confirmed only; Cancelled is terminal for confirm.
    public void Confirm()
    {
        if (Status != "Draft")
            throw new InvalidStateProblem($"Order {Code} is {Status}; only Draft orders can be confirmed.");
        Status = "Confirmed";
    }

    // Write-side state machine: Draft/Confirmed→Cancelled only; Cancelled is final.
    public void Cancel()
    {
        if (Status == "Cancelled")
            throw new InvalidStateProblem($"Order {Code} is already Cancelled; a cancelled order is final.");
        Status = "Cancelled";
    }
}
