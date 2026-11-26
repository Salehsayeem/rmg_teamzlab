using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Merchandising.Domain;

namespace Rmg.Modules.Merchandising.Infrastructure;

// ord.buyer — baseline mapped inline (P-2 style, like org.legal_entity): Buyer carries NO global
// query filter, so reads stay correct on both the unfiltered RmgDbContext (login, seeding, harness)
// and the scoped TenantScopedDbContext — every repository query pins the tenant explicitly instead.
public sealed class BuyerConfiguration : IEntityTypeConfiguration<Buyer>
{
    public void Configure(EntityTypeBuilder<Buyer> b)
    {
        b.ToTable("buyer", "ord");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Country).HasColumnName("country").HasColumnType("char(2)").HasMaxLength(2);
        b.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").HasMaxLength(3);
        b.Property(x => x.Status).HasColumnName("status");
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // xmin optimistic concurrency — same convention TenantEntityConfiguration uses (Npgsql 10+).
        b.Property<uint>("xmin").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
    }
}

// ord.buyer_contact — the composite FK pins a contact to its buyer within the same tenant (R-6);
// HasPrincipalKey adds the (tenant_id, id) unique key ord.buyer needs as the FK target.
public sealed class BuyerContactConfiguration : IEntityTypeConfiguration<BuyerContact>
{
    public void Configure(EntityTypeBuilder<BuyerContact> b)
    {
        b.ToTable("buyer_contact", "ord");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.BuyerId).HasColumnName("buyer_id");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Email).HasColumnName("email");
        b.Property(x => x.Phone).HasColumnName("phone");
        b.HasIndex(x => new { x.TenantId, x.BuyerId, x.Email }).IsUnique();
        b.HasOne<Buyer>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.BuyerId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property<uint>("xmin").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
    }
}
