using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.ProductEngineering.Domain;

// pdm.style_version — the immutable-once-referenced revision of a style (pilot: version 1 only).
public class StyleVersion : TenantEntity
{
    public Guid StyleId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? ApprovedAtUtc { get; set; }

    // Write-side state machine: Draft→Approved pins the approval time; Approved/Superseded are final.
    public void Approve(DateTime approvedAtUtc)
    {
        if (Status != "Draft")
            throw new InvalidStateProblem($"Style version {Version} is {Status}; only Draft versions can be approved.");
        Status = "Approved";
        ApprovedAtUtc = approvedAtUtc;
    }
}
