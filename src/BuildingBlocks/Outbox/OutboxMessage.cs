namespace Rmg.BuildingBlocks.Outbox;

// outbox.outbox_message — transactional event written with the state change, dispatched by the worker.
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public int SchemaVersion { get; set; }
    public Guid TenantId { get; set; }
    public Guid AggregateId { get; set; }
    public string Actor { get; set; } = null!;
    public string CorrelationId { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public string Payload { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
}
