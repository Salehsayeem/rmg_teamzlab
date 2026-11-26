using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Merchandising.Domain;

// Task 14 (ORD) — customer order + lines + sizes + schedules (ord schema, all on the TenantEntity
// baseline; every unique key and composite FK carries tenant_id per R-6, the principals declare the
// matching (tenant_id, id) alternate keys). The cross-module FKs (buyer_id → ord.buyer, style_id →
// pdm.style, style_version_id → pdm.style_version) are raw SQL in 005_OrdOrder (ruling W2-b: no EF
// navigation across modules).
public sealed class CustomerOrderConfiguration : TenantEntityConfiguration<CustomerOrder>
{
    public override void Configure(EntityTypeBuilder<CustomerOrder> b)
    {
        base.Configure(b);
        b.ToTable("customer_order", "ord", tb => tb.HasCheckConstraint(
            "ck_customer_order_status", "status IN ('Draft', 'Confirmed', 'Cancelled')"));
        b.Property(x => x.FacilityId).HasColumnName("facility_id");
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.BuyerId).HasColumnName("buyer_id");
        b.Property(x => x.BuyerPo).HasColumnName("buyer_po");
        b.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").HasMaxLength(3);
        b.Property(x => x.OrderDate).HasColumnName("order_date");
        b.Property(x => x.Status).HasColumnName("status");
        b.HasIndex(x => new { x.TenantId, x.BuyerId, x.BuyerPo }).IsUnique();
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
    }
}

public sealed class OrderLineConfiguration : TenantEntityConfiguration<OrderLine>
{
    public override void Configure(EntityTypeBuilder<OrderLine> b)
    {
        base.Configure(b);
        b.ToTable("order_line", "ord", tb => tb.HasCheckConstraint(
            "ck_order_line_price_non_negative", "price >= 0"));
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.LineNo).HasColumnName("line_no");
        b.Property(x => x.StyleId).HasColumnName("style_id");
        b.Property(x => x.StyleVersionId).HasColumnName("style_version_id");
        b.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(18,2)");
        b.Property(x => x.LineQty).HasColumnName("line_qty").HasColumnType("numeric(18,0)");
        b.HasIndex(x => new { x.TenantId, x.OrderId, x.LineNo }).IsUnique();
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.HasOne<CustomerOrder>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

public sealed class SizeQuantityConfiguration : TenantEntityConfiguration<SizeQuantity>
{
    public override void Configure(EntityTypeBuilder<SizeQuantity> b)
    {
        base.Configure(b);
        b.ToTable("size_quantity", "ord", tb => tb.HasCheckConstraint(
            "ck_size_quantity_qty_positive", "qty > 0"));
        b.Property(x => x.OrderLineId).HasColumnName("order_line_id");
        b.Property(x => x.ColorCode).HasColumnName("color_code");
        b.Property(x => x.SizeCode).HasColumnName("size_code");
        b.Property(x => x.Qty).HasColumnName("qty").HasColumnType("numeric(18,0)");
        b.HasIndex(x => new { x.TenantId, x.OrderLineId, x.ColorCode, x.SizeCode }).IsUnique();
        b.HasOne<OrderLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrderLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}

public sealed class DeliveryScheduleConfiguration : TenantEntityConfiguration<DeliverySchedule>
{
    public override void Configure(EntityTypeBuilder<DeliverySchedule> b)
    {
        base.Configure(b);
        b.ToTable("delivery_schedule", "ord", tb => tb.HasCheckConstraint(
            "ck_delivery_schedule_qty_positive", "qty > 0"));
        b.Property(x => x.OrderLineId).HasColumnName("order_line_id");
        b.Property(x => x.ShipDate).HasColumnName("ship_date");
        b.Property(x => x.Qty).HasColumnName("qty").HasColumnType("numeric(18,0)");
        b.HasOne<OrderLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrderLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}
