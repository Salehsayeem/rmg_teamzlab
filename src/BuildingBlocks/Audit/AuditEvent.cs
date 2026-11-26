namespace Rmg.BuildingBlocks.Audit;

// audit.audit_event — who changed what, when, and why.
public class AuditEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? FacilityId { get; set; }
    public string ActorId { get; set; } = null!;
    public string ActorDisplay { get; set; } = null!;
    public string Command { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string ChangedFields { get; set; } = null!;
    public string? Reason { get; set; }
    public string CorrelationId { get; set; } = null!;
    public string? ApprovalRef { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
