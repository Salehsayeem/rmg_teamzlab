using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rmg.Modules.Organization.Domain;

namespace Rmg.Modules.Organization.Infrastructure;

// Baseline columns are mapped inline per entity (P-2); the shared TenantEntity base arrives in Task 7.
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenant", "org");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Code).HasColumnName("code");
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.DefaultLocale).HasColumnName("default_locale");
        b.Property(x => x.RequireMakerChecker).HasColumnName("require_maker_checker");
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}

public sealed class LegalEntityConfiguration : IEntityTypeConfiguration<LegalEntity>
{
    public void Configure(EntityTypeBuilder<LegalEntity> b)
    {
        b.ToTable("legal_entity", "org");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Country).HasColumnName("country").HasColumnType("char(2)").HasMaxLength(2);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}

public sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> b)
    {
        b.ToTable("facility", "org");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id");
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Timezone).HasColumnName("timezone");
        b.Property(x => x.Address).HasColumnName("address");
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        // R-6: composite FK carries tenant_id; principal needs the matching (tenant_id, id) unique key.
        b.HasOne<LegalEntity>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}

public sealed class TenantLocalePolicyConfiguration : IEntityTypeConfiguration<TenantLocalePolicy>
{
    public void Configure(EntityTypeBuilder<TenantLocalePolicy> b)
    {
        b.ToTable("tenant_locale_policy", "org");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Locale).HasColumnName("locale");
        b.Property(x => x.IsDefault).HasColumnName("is_default");
        b.HasIndex(x => new { x.TenantId, x.Locale }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}
