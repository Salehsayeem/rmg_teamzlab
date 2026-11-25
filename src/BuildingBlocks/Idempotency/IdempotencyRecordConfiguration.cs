using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rmg.BuildingBlocks.Idempotency;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_record", "iam");
        b.HasKey(x => new { x.TenantId, x.Endpoint, x.KeyHash });
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Endpoint).HasColumnName("endpoint");
        b.Property(x => x.KeyHash).HasColumnName("key_hash");
        b.Property(x => x.RequestHash).HasColumnName("request_hash");
        b.Property(x => x.ResponseStatus).HasColumnName("response_status");
        b.Property(x => x.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
    }
}
