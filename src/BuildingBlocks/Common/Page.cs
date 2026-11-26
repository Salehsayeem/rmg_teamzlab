namespace Rmg.BuildingBlocks.Common;

// R-1: collection reads use cursor pagination with a hard pageSize cap.
public sealed record Page
{
    public static (int PageSize, string? Cursor) Normalize(int pageSize, string? cursor) =>
        (Math.Clamp(pageSize, 1, 100), string.IsNullOrWhiteSpace(cursor) ? null : cursor);
}

public sealed record PageMeta(int PageSize, string? NextCursor, int? Total);
