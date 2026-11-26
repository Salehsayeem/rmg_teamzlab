using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.Modules.ProductEngineering.Domain;

// pdm.bom — one per style version (unique (tenant_id, style_version_id)); Draft→Released is final.
public class Bom : TenantEntity
{
    public Guid StyleVersionId { get; set; }
    public string Status { get; set; } = "Draft";

    // Write-side state machine: Draft→Released only, and only with at least one line.
    public void Release(int lineCount)
    {
        if (Status != "Draft")
            throw new InvalidStateProblem($"BOM is {Status}; only Draft BOMs can be released.");
        if (lineCount < 1)
            throw new BusinessRuleProblem("NO_LINES", "A BOM needs at least one line before release.");
        Status = "Released";
    }
}
