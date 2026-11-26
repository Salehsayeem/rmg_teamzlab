namespace Rmg.BuildingBlocks.Outbox;

/// <summary>Enqueues a transactional outbox message into the caller's current DbContext change set —
/// no SaveChanges, the command handler owns the transaction. The dispatcher delivers it later.</summary>
public interface IOutboxWriter
{
    Task EnqueueAsync(string eventType, object payload, Guid aggregateId);
}
