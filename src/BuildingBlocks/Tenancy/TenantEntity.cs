using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rmg.BuildingBlocks.Tenancy;

/// <summary>Shared base for tenant-scoped entities: the baseline columns every table carries. Mapped by
/// <see cref="TenantEntityConfiguration{TEntity}"/> (xmin concurrency token + tenant query filter);
/// facility moves onto it per ruling P-10 — other T5 entities keep inline columns until their own
/// task (P-2). Property names/types must stay stable: the M001 schema already has these columns.</summary>
public abstract class TenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Baseline.
    public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Ambient request tenant — the global query filter below reads it once per query execution.
    /// Exposed as a property (not a field): EF constantizes static field reads into the cached query plan,
    /// while a property getter is re-evaluated per execution. The Api mirrors ITenantContext into it per
    /// request (Program.cs guard block). Null = fail closed: queries match no rows, so non-HTTP callers
    /// set it explicitly or bypass with IgnoreQueryFilters().</summary>
    private static readonly AsyncLocal<Guid?> Ambient = new();

    public static Guid? AmbientTenantId => Ambient.Value;

    public static void SetAmbientTenant(Guid? tenantId) => Ambient.Value = tenantId;
}

// Shared baseline mapping + xmin optimistic concurrency + the tenant query filter.
public class TenantEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : TenantEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> b)
    {
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // xmin is a PostgreSQL system column — a concurrency token without any table change. (Npgsql 10
        // removed UseXminAsConcurrencyToken: uint + OnAddOrUpdate + concurrency token is its xmin convention.)
        b.Property<uint>("xmin").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();

        // Fail-closed default: EF constantizes query filters — a plain static/ambient read here would be
        // baked into the cached query plan at first execution (verified empirically), so a tenant filter
        // cannot live on a static. Matches no rows, so direct RmgDbContext users see nothing rather than
        // another tenant's data; TenantScopedDbContext (Database project) replaces this with the
        // context-rooted filter that re-evaluates per request.
        b.HasQueryFilter(_ => false);
    }
}
