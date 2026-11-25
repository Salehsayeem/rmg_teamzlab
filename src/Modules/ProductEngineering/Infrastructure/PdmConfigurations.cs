using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Modules.ProductEngineering.Infrastructure;

// Global reference data: code is the key, no tenant scoping. The six pilot codes are seeded by the
// 003_PdmMaster migration (InsertData), so a scratch `db reset` restores them.
public sealed class UomConfiguration : IEntityTypeConfiguration<Uom>
{
    public void Configure(EntityTypeBuilder<Uom> b)
    {
        b.ToTable("uom", "pdm");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Name).HasColumnName("name");
    }
}

// Material rides the shared TenantEntity baseline (P-10); only its own columns and the UOM relation remain here.
public sealed class MaterialConfiguration : TenantEntityConfiguration<Material>
{
    public override void Configure(EntityTypeBuilder<Material> b)
    {
        base.Configure(b);
        b.ToTable("material", "pdm");
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Category).HasColumnName("category");
        b.Property(x => x.BaseUomCode).HasColumnName("base_uom_code");
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        // Reference-data FK: a material's base UOM must exist in the global catalogue.
        b.HasOne<Uom>().WithMany().HasForeignKey(x => x.BaseUomCode);
    }
}
