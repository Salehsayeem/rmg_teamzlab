using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.ProductEngineering.Domain;

// pdm.style — master data on the shared TenantEntity baseline (P-10). buyer_id references
// ord.buyer through a raw-SQL composite FK (ruling W2-b: no EF navigation across modules).
public class Style : TenantEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Season { get; set; }
    public string? Category { get; set; }
    public Guid BuyerId { get; set; }

    // Server-set lifecycle; clients never set status directly.
    public string Status { get; set; } = "Draft";

    // Write-side state machine: Draft→Approved only, and only with at least one variant.
    public void Approve(int variantCount)
    {
        if (Status != "Draft")
            throw new InvalidStateProblem($"Style {Code} is {Status}; only Draft styles can be approved.");
        if (variantCount < 1)
            throw new BusinessRuleProblem("NO_VARIANTS", "A style needs at least one variant before approval.");
        Status = "Approved";
    }
}
