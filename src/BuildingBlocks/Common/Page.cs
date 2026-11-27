namespace Rmg.BuildingBlocks.Common;

// R-1: collection reads use cursor pagination with a hard pageSize cap.
public sealed record Page
{
    // Matches the web clients' DEFAULT_PAGE_SIZE so raw API calls without
    // pageSize serve a usable page instead of a single clamped row.
    public const int DefaultSize = 20;

    public static (int PageSize, string? Cursor) Normalize(int pageSize, string? cursor)
    {
        // An omitted query param binds as 0 — serve the default page, not one row.
        if (pageSize <= 0) pageSize = DefaultSize;
        return (Math.Clamp(pageSize, 1, 100), string.IsNullOrWhiteSpace(cursor) ? null : cursor);
    }
}

public sealed record PageMeta(int PageSize, string? NextCursor, int? Total);
