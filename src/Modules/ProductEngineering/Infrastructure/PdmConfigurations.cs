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
        // (tenant_id, id) is the FK target of pdm.bom_line's composite material key (R-6).
        b.HasAlternateKey(x => new { x.TenantId, x.Id });

        // Reference-data FK: a material's base UOM must exist in the global catalogue.
        b.HasOne<Uom>().WithMany().HasForeignKey(x => x.BaseUomCode);
    }
}

// Task 12 (PDM) — style master + version + variant (pdm schema, all on the TenantEntity baseline;
// every composite FK carries tenant_id per R-6, the principals declare the matching (tenant_id, id)
// alternate keys). pdm.style.buyer_id points at ord.buyer via raw SQL in 004_PdmStyle (ruling W2-b:
// no EF navigation across modules).
public sealed class StyleConfiguration : TenantEntityConfiguration<Style>
{
    public override void Configure(EntityTypeBuilder<Style> b)
    {
        base.Configure(b);
        b.ToTable("style", "pdm", tb => tb.HasCheckConstraint(
            "ck_style_status", "status IN ('Draft', 'Approved')"));
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Season).HasColumnName("season");
        b.Property(x => x.Category).HasColumnName("category");
        b.Property(x => x.BuyerId).HasColumnName("buyer_id");
        b.Property(x => x.Status).HasColumnName("status");
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
    }
}

public sealed class StyleVersionConfiguration : TenantEntityConfiguration<StyleVersion>
{
    public override void Configure(EntityTypeBuilder<StyleVersion> b)
    {
        base.Configure(b);
        b.ToTable("style_version", "pdm", tb => tb.HasCheckConstraint(
            "ck_style_version_status", "status IN ('Draft', 'Approved', 'Superseded')"));
        b.Property(x => x.StyleId).HasColumnName("style_id");
        b.Property(x => x.Version).HasColumnName("version");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.ApprovedAtUtc).HasColumnName("approved_at_utc");
        b.HasIndex(x => new { x.TenantId, x.StyleId, x.Version }).IsUnique();
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.HasOne<Style>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.StyleId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

public sealed class StyleVariantConfiguration : TenantEntityConfiguration<StyleVariant>
{
    public override void Configure(EntityTypeBuilder<StyleVariant> b)
    {
        base.Configure(b);
        b.ToTable("style_variant", "pdm");
        b.Property(x => x.StyleVersionId).HasColumnName("style_version_id");
        b.Property(x => x.ColorCode).HasColumnName("color_code");
        b.Property(x => x.SizeCode).HasColumnName("size_code");
        b.HasIndex(x => new { x.TenantId, x.StyleVersionId, x.ColorCode, x.SizeCode }).IsUnique();
        b.HasOne<StyleVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.StyleVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

// Task 13 (PDM) — BOM header + lines: one BOM per style version; lines frozen once Released.
public sealed class BomConfiguration : TenantEntityConfiguration<Bom>
{
    public override void Configure(EntityTypeBuilder<Bom> b)
    {
        base.Configure(b);
        b.ToTable("bom", "pdm", tb => tb.HasCheckConstraint(
            "ck_bom_status", "status IN ('Draft', 'Released')"));
        b.Property(x => x.StyleVersionId).HasColumnName("style_version_id");
        b.Property(x => x.Status).HasColumnName("status");
        b.HasIndex(x => new { x.TenantId, x.StyleVersionId }).IsUnique();
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.HasOne<StyleVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.StyleVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

public sealed class BomLineConfiguration : TenantEntityConfiguration<BomLine>
{
    public override void Configure(EntityTypeBuilder<BomLine> b)
    {
        base.Configure(b);
        b.ToTable("bom_line", "pdm", tb => tb.HasCheckConstraint(
            "ck_bom_line_qty_positive", "qty_per_unit > 0"));
        b.Property(x => x.BomId).HasColumnName("bom_id");
        b.Property(x => x.MaterialId).HasColumnName("material_id");
        b.Property(x => x.QtyPerUnit).HasColumnName("qty_per_unit").HasColumnType("numeric(18,4)");
        b.Property(x => x.UomCode).HasColumnName("uom_code");
        b.Property(x => x.WastePct).HasColumnName("waste_pct").HasColumnType("numeric(5,2)").HasDefaultValue(0m);
        b.HasOne<Bom>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.BomId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.HasOne<Material>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MaterialId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
        // Reference-data FK: a line's UOM must exist in the global catalogue (like material.base_uom_code).
        b.HasOne<Uom>().WithMany().HasForeignKey(x => x.UomCode);
    }
}
