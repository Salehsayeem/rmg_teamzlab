using Rmg.BuildingBlocks.Common;

namespace Rmg.UnitTests;

// Cursor pagination contract (R-1): an omitted or non-positive pageSize binds
// as 0 and must serve the default page size (matching the web's
// DEFAULT_PAGE_SIZE), never a single row; the hard cap 100 still applies.
public class PageTests
{
    [Fact]
    public void Normalize_Omitted_PageSize_Serves_Default()
    {
        Assert.Equal((20, (string?)null), Page.Normalize(0, null));
    }

    [Fact]
    public void Normalize_Explicit_PageSize_Clamps_To_Cap()
    {
        Assert.Equal((100, (string?)null), Page.Normalize(150, null));
        Assert.Equal((1, (string?)null), Page.Normalize(1, null));
    }

    [Fact]
    public void Normalize_NonPositive_PageSize_Serves_Default()
    {
        Assert.Equal((20, (string?)null), Page.Normalize(-5, null));
    }

    [Fact]
    public void Normalize_Blank_Cursor_Becomes_Null()
    {
        Assert.Equal((20, (string?)null), Page.Normalize(20, " "));
    }
}
