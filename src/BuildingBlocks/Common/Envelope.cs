namespace Rmg.BuildingBlocks.Common;

// R-1: every success response is { data, meta }; nothing else.
public sealed record Envelope<T>(T Data, Dictionary<string, object>? Meta)
{
    public static Envelope<T> Of(T data, object? meta = null) => new(data, AsMeta(meta));

    private static Dictionary<string, object>? AsMeta(object? meta) => meta switch
    {
        null => null,
        Dictionary<string, object> dict => dict,
        // non-dictionary meta (e.g. PageMeta) is preserved under a "meta" key rather than dropped
        _ => new Dictionary<string, object> { ["meta"] = meta }
    };
}
