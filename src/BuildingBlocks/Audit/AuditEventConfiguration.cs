using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rmg.BuildingBlocks.Audit;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_event", "audit");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.FacilityId).HasColumnName("facility_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.ActorDisplay).HasColumnName("actor_display");
        b.Property(x => x.Command).HasColumnName("command");
        b.Property(x => x.EntityType).HasColumnName("entity_type");
        b.Property(x => x.EntityId).HasColumnName("entity_id");
        b.Property(x => x.ChangedFields).HasColumnName("changed_fields").HasColumnType("jsonb");
        b.Property(x => x.Reason).HasColumnName("reason");
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        b.Property(x => x.ApprovalRef).HasColumnName("approval_ref");
        b.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        b.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
    }
}
