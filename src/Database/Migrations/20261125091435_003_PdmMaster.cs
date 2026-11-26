using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rmg.Database.Migrations
{
    /// <inheritdoc />
    public partial class _003_PdmMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pdm");

            migrationBuilder.CreateTable(
                name: "uom",
                schema: "pdm",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uom", x => x.code);
                });

            // Global reference data: the six pilot UOMs live in the migration (not Seeder.cs), so a
            // `db reset` — which re-applies migrations — restores them automatically.
            migrationBuilder.InsertData(
                schema: "pdm",
                table: "uom",
                columns: new[] { "code", "name" },
                values: new object[,]
                {
                    { "DOZ", "Dozen" },
                    { "KG", "Kilogram" },
                    { "M", "Meter" },
                    { "M2", "Square Meter" },
                    { "PC", "Piece" },
                    { "YD", "Yard" }
                });

            migrationBuilder.CreateTable(
                name: "material",
                schema: "pdm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: true),
                    base_uom_code = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_material", x => x.id);
                    table.ForeignKey(
                        name: "FK_material_uom_base_uom_code",
                        column: x => x.base_uom_code,
                        principalSchema: "pdm",
                        principalTable: "uom",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_material_base_uom_code",
                schema: "pdm",
                table: "material",
                column: "base_uom_code");

            migrationBuilder.CreateIndex(
                name: "IX_material_tenant_id_code",
                schema: "pdm",
                table: "material",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "material",
                schema: "pdm");

            migrationBuilder.DropTable(
                name: "uom",
                schema: "pdm");
        }
    }
}
