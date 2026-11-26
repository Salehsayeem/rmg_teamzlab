namespace Rmg.BuildingBlocks.Audit;

/// <summary>Records an audit event into the caller's current DbContext change set —
/// no SaveChanges here, the command handler owns the transaction.</summary>
public interface IAuditWriter
{
    Task WriteAsync(string command, string entityType, Guid entityId, object? before, object? after, string? reason = null);
}
