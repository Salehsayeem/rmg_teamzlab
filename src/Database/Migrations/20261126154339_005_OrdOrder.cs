using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rmg.Database.Migrations
{
    /// <inheritdoc />
    public partial class _005_OrdOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_order",
                schema: "ord",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    facility_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "text", nullable: false),
                    buyer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_po = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_order", x => x.id);
                    table.UniqueConstraint("AK_customer_order_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_customer_order_status", "status IN ('Draft', 'Confirmed', 'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "order_line",
                schema: "ord",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    style_id = table.Column<Guid>(type: "uuid", nullable: false),
                    style_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    line_qty = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_line", x => x.id);
                    table.UniqueConstraint("AK_order_line_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_order_line_price_non_negative", "price >= 0");
                    table.ForeignKey(
                        name: "FK_order_line_customer_order_tenant_id_order_id",
                        columns: x => new { x.tenant_id, x.order_id },
                        principalSchema: "ord",
                        principalTable: "customer_order",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_schedule",
                schema: "ord",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ship_date = table.Column<DateOnly>(type: "date", nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_schedule", x => x.id);
                    table.CheckConstraint("ck_delivery_schedule_qty_positive", "qty > 0");
                    table.ForeignKey(
                        name: "FK_delivery_schedule_order_line_tenant_id_order_line_id",
                        columns: x => new { x.tenant_id, x.order_line_id },
                        principalSchema: "ord",
                        principalTable: "order_line",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "size_quantity",
                schema: "ord",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    color_code = table.Column<string>(type: "text", nullable: false),
                    size_code = table.Column<string>(type: "text", nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_size_quantity", x => x.id);
                    table.CheckConstraint("ck_size_quantity_qty_positive", "qty > 0");
                    table.ForeignKey(
                        name: "FK_size_quantity_order_line_tenant_id_order_line_id",
                        columns: x => new { x.tenant_id, x.order_line_id },
                        principalSchema: "ord",
                        principalTable: "order_line",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_order_tenant_id_buyer_id_buyer_po",
                schema: "ord",
                table: "customer_order",
                columns: new[] { "tenant_id", "buyer_id", "buyer_po" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_schedule_tenant_id_order_line_id",
                schema: "ord",
                table: "delivery_schedule",
                columns: new[] { "tenant_id", "order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_order_line_tenant_id_order_id_line_no",
                schema: "ord",
                table: "order_line",
                columns: new[] { "tenant_id", "order_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_size_quantity_tenant_id_order_line_id_color_code_size_code",
                schema: "ord",
                table: "size_quantity",
                columns: new[] { "tenant_id", "order_line_id", "color_code", "size_code" },
                unique: true);

            // Ruling W2-b: the composite FKs to ord.buyer / pdm.style / pdm.style_version are
            // cross-module (modules reference Common+Tenancy only, no EF navigation), so they are
            // raw SQL. The (tenant_id, id) targets: ord.buyer AK from 002_OrdBuyer, pdm.style /
            // pdm.style_version AKs from 004_PdmStyle. The FKs alone enforce existence (pilot: no
            // service-level buyer/style existence checks).
            migrationBuilder.Sql(
                "ALTER TABLE ord.customer_order ADD CONSTRAINT fk_customer_order_buyer " +
                "FOREIGN KEY (tenant_id, buyer_id) REFERENCES ord.buyer (tenant_id, id);");
            migrationBuilder.Sql(
                "ALTER TABLE ord.order_line ADD CONSTRAINT fk_order_line_style " +
                "FOREIGN KEY (tenant_id, style_id) REFERENCES pdm.style (tenant_id, id);");
            migrationBuilder.Sql(
                "ALTER TABLE ord.order_line ADD CONSTRAINT fk_order_line_style_version " +
                "FOREIGN KEY (tenant_id, style_version_id) REFERENCES pdm.style_version (tenant_id, id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ruling W2-b mirrors of the raw-SQL FKs added in Up, before the local DropTables.
            migrationBuilder.Sql(
                "ALTER TABLE ord.order_line DROP CONSTRAINT fk_order_line_style_version;");
            migrationBuilder.Sql(
                "ALTER TABLE ord.order_line DROP CONSTRAINT fk_order_line_style;");
            migrationBuilder.Sql(
                "ALTER TABLE ord.customer_order DROP CONSTRAINT fk_customer_order_buyer;");

            migrationBuilder.DropTable(
                name: "delivery_schedule",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "size_quantity",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "order_line",
                schema: "ord");

            migrationBuilder.DropTable(
                name: "customer_order",
                schema: "ord");
        }
    }
}
