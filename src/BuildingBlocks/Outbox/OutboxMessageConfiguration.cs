using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rmg.BuildingBlocks.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_message", "outbox", tb => tb.HasCheckConstraint(
            "ck_outbox_message_status", "status IN ('Pending', 'Processed', 'Failed')"));
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.EventType).HasColumnName("event_type");
        b.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.AggregateId).HasColumnName("aggregate_id");
        b.Property(x => x.Actor).HasColumnName("actor");
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id");
        b.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        b.Property(x => x.RecordedAtUtc).HasColumnName("recorded_at_utc");
        b.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");

        // Dispatcher poll: only pending rows, oldest scheduled first.
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc }).HasFilter("status = 'Pending'");
    }
}
