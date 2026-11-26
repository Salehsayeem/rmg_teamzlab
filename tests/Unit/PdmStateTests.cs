using Rmg.BuildingBlocks.Common;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.UnitTests;

// Task 12 (PDM): the style/BOM state machines live on the entities — the write-side guards. Moving
// outside the allowed state throws InvalidStateProblem; the release/approve children guards throw
// their business-rule codes. Table-tested across every entity × status.
public class PdmStateTests
{
    [Fact]
    public void Style_Approve_Twice_Throws()
    {
        var style = new Style { Status = "Draft" };

        style.Approve(1); // one variant — the allowed Draft→Approved move

        Assert.Equal("Approved", style.Status);
        Assert.Throws<InvalidStateProblem>(() => style.Approve(1)); // already Approved
    }

    [Fact]
    public void Bom_Release_WithoutLines_Throws()
    {
        var bom = new Bom { Status = "Draft" };

        Assert.Throws<BusinessRuleProblem>(() => bom.Release(0)); // guard: ≥1 line
        Assert.Equal("Draft", bom.Status); // nothing moved on the rejected write

        bom.Release(1);
        Assert.Throws<InvalidStateProblem>(() => bom.Release(2)); // Released is terminal
    }

    [Theory]
    [InlineData("Draft", 1, true)]
    [InlineData("Draft", 0, false)] // NO_VARIANTS
    [InlineData("Approved", 1, false)] // INVALID_STATE
    [InlineData("Superseded", 1, false)] // INVALID_STATE
    public void Style_State_Table(string status, int variants, bool allowed)
    {
        var style = new Style { Status = status };

        if (allowed)
        {
            style.Approve(variants);
            Assert.Equal("Approved", style.Status);
        }
        else
        {
            Assert.ThrowsAny<RmgProblemException>(() => style.Approve(variants));
        }
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Approved", false)]
    [InlineData("Superseded", false)]
    public void StyleVersion_State_Table(string status, bool allowed)
    {
        var version = new StyleVersion { Status = status };

        if (allowed)
        {
            version.Approve(DateTime.UtcNow);
            Assert.Equal("Approved", version.Status);
            Assert.NotNull(version.ApprovedAtUtc);
        }
        else
        {
            Assert.Throws<InvalidStateProblem>(() => version.Approve(DateTime.UtcNow));
        }
    }

    [Theory]
    [InlineData("Draft", 1, true)]
    [InlineData("Draft", 0, false)] // NO_LINES
    [InlineData("Released", 1, false)] // INVALID_STATE
    public void Bom_State_Table(string status, int lines, bool allowed)
    {
        var bom = new Bom { Status = status };

        if (allowed)
        {
            bom.Release(lines);
            Assert.Equal("Released", bom.Status);
        }
        else
        {
            Assert.ThrowsAny<RmgProblemException>(() => bom.Release(lines));
        }
    }
}
