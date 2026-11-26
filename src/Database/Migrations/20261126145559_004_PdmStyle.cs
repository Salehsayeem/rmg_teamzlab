using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rmg.Database.Migrations
{
    /// <inheritdoc />
    public partial class _004_PdmStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_material_tenant_id_id",
                schema: "pdm",
                table: "material",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "style",
                schema: "pdm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    season = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: true),
                    buyer_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_style", x => x.id);
                    table.UniqueConstraint("AK_style_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_style_status", "status IN ('Draft', 'Approved')");
                });

            migrationBuilder.CreateTable(
                name: "style_version",
                schema: "pdm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    style_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_style_version", x => x.id);
                    table.UniqueConstraint("AK_style_version_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_style_version_status", "status IN ('Draft', 'Approved', 'Superseded')");
                    table.ForeignKey(
                        name: "FK_style_version_style_tenant_id_style_id",
                        columns: x => new { x.tenant_id, x.style_id },
                        principalSchema: "pdm",
                        principalTable: "style",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bom",
                schema: "pdm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    style_version_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_bom", x => x.id);
                    table.UniqueConstraint("AK_bom_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_bom_status", "status IN ('Draft', 'Released')");
                    table.ForeignKey(
                        name: "FK_bom_style_version_tenant_id_style_version_id",
                        columns: x => new { x.tenant_id, x.style_version_id },
                        principalSchema: "pdm",
                        principalTable: "style_version",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "style_variant",
                schema: "pdm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    style_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    color_code = table.Column<string>(type: "text", nullable: false),
                    size_code = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_style_variant", x => x.id);
                    table.ForeignKey(
                        name: "FK_style_variant_style_version_tenant_id_style_version_id",
                        columns: x => new { x.tenant_id, x.style_version_id },
                        principalSchema: "pdm",
                        principalTable: "style_version",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bom_line",
                schema: "pdm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty_per_unit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    uom_code = table.Column<string>(type: "text", nullable: false),
                    waste_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
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
                    table.PrimaryKey("PK_bom_line", x => x.id);
                    table.CheckConstraint("ck_bom_line_qty_positive", "qty_per_unit > 0");
                    table.ForeignKey(
                        name: "FK_bom_line_bom_tenant_id_bom_id",
                        columns: x => new { x.tenant_id, x.bom_id },
                        principalSchema: "pdm",
                        principalTable: "bom",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bom_line_material_tenant_id_material_id",
                        columns: x => new { x.tenant_id, x.material_id },
                        principalSchema: "pdm",
                        principalTable: "material",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bom_line_uom_uom_code",
                        column: x => x.uom_code,
                        principalSchema: "pdm",
                        principalTable: "uom",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bom_tenant_id_style_version_id",
                schema: "pdm",
                table: "bom",
                columns: new[] { "tenant_id", "style_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bom_line_tenant_id_bom_id",
                schema: "pdm",
                table: "bom_line",
                columns: new[] { "tenant_id", "bom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bom_line_tenant_id_material_id",
                schema: "pdm",
                table: "bom_line",
                columns: new[] { "tenant_id", "material_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bom_line_uom_code",
                schema: "pdm",
                table: "bom_line",
                column: "uom_code");

            migrationBuilder.CreateIndex(
                name: "IX_style_tenant_id_code",
                schema: "pdm",
                table: "style",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_style_variant_tenant_id_style_version_id_color_code_size_co~",
                schema: "pdm",
                table: "style_variant",
                columns: new[] { "tenant_id", "style_version_id", "color_code", "size_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_style_version_tenant_id_style_id_version",
                schema: "pdm",
                table: "style_version",
                columns: new[] { "tenant_id", "style_id", "version" },
                unique: true);

            // Ruling W2-b: the composite FK to ord.buyer is cross-module (modules reference Common+
            // Tenancy only, no EF navigation), so it is raw SQL — the ord.buyer (tenant_id, id) AK
            // comes from 002_OrdBuyer. The FK alone enforces buyer existence (pilot: no service check).
            migrationBuilder.Sql(
                "ALTER TABLE pdm.style ADD CONSTRAINT fk_style_buyer " +
                "FOREIGN KEY (tenant_id, buyer_id) REFERENCES ord.buyer (tenant_id, id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bom_line",
                schema: "pdm");

            migrationBuilder.DropTable(
                name: "style_variant",
                schema: "pdm");

            migrationBuilder.DropTable(
                name: "bom",
                schema: "pdm");

            migrationBuilder.DropTable(
                name: "style_version",
                schema: "pdm");

            // Ruling W2-b mirror of the raw-SQL FK added in Up.
            migrationBuilder.Sql(
                "ALTER TABLE pdm.style DROP CONSTRAINT fk_style_buyer;");

            migrationBuilder.DropTable(
                name: "style",
                schema: "pdm");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_material_tenant_id_id",
                schema: "pdm",
                table: "material");
        }
    }
}
