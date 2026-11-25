using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rmg.Database.Migrations
{
    /// <inheritdoc />
    public partial class _001_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "org");

            migrationBuilder.EnsureSchema(
                name: "outbox");

            migrationBuilder.CreateTable(
                name: "app_user",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                    table.UniqueConstraint("AK_app_user_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "audit_event",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    facility_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<string>(type: "text", nullable: false),
                    actor_display = table.Column<string>(type: "text", nullable: false),
                    command = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    changed_fields = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<string>(type: "text", nullable: false),
                    approval_ref = table.Column<string>(type: "text", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_record",
                schema: "iam",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    key_hash = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_record", x => new { x.tenant_id, x.endpoint, x.key_hash });
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message", x => x.id);
                    table.CheckConstraint("ck_outbox_message_status", "status IN ('Pending', 'Processed', 'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "permission",
                schema: "iam",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "role",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.id);
                    table.UniqueConstraint("AK_role_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    default_locale = table.Column<string>(type: "text", nullable: false),
                    require_maker_checker = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permission",
                schema: "iam",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permission", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permission_permission_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "iam",
                        principalTable: "permission",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permission_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "iam",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                schema: "iam",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role", x => new { x.tenant_id, x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_role_app_user_tenant_id_user_id",
                        columns: x => new { x.tenant_id, x.user_id },
                        principalSchema: "iam",
                        principalTable: "app_user",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_role_role_tenant_id_role_id",
                        columns: x => new { x.tenant_id, x.role_id },
                        principalSchema: "iam",
                        principalTable: "role",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "legal_entity",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    country = table.Column<string>(type: "char(2)", maxLength: 2, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_entity", x => x.id);
                    table.UniqueConstraint("AK_legal_entity_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_legal_entity_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "org",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_locale_policy",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_locale_policy", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_locale_policy_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "org",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "facility",
                schema: "org",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facility", x => x.id);
                    table.ForeignKey(
                        name: "FK_facility_legal_entity_tenant_id_legal_entity_id",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "org",
                        principalTable: "legal_entity",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_user_tenant_id_email",
                schema: "iam",
                table: "app_user",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_tenant_id_occurred_at_utc",
                schema: "audit",
                table: "audit_event",
                columns: new[] { "tenant_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_facility_tenant_id_code",
                schema: "org",
                table: "facility",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_facility_tenant_id_legal_entity_id",
                schema: "org",
                table: "facility",
                columns: new[] { "tenant_id", "legal_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_entity_tenant_id_code",
                schema: "org",
                table: "legal_entity",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_status_next_attempt_at_utc",
                schema: "outbox",
                table: "outbox_message",
                columns: new[] { "status", "next_attempt_at_utc" },
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_role_tenant_id_code",
                schema: "iam",
                table: "role",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permission_permission_id",
                schema: "iam",
                table: "role_permission",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_code",
                schema: "org",
                table: "tenant",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_locale_policy_tenant_id_locale",
                schema: "org",
                table: "tenant_locale_policy",
                columns: new[] { "tenant_id", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_role_tenant_id_role_id",
                schema: "iam",
                table: "user_role",
                columns: new[] { "tenant_id", "role_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_event",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "facility",
                schema: "org");

            migrationBuilder.DropTable(
                name: "idempotency_record",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "outbox");

            migrationBuilder.DropTable(
                name: "role_permission",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "tenant_locale_policy",
                schema: "org");

            migrationBuilder.DropTable(
                name: "user_role",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "legal_entity",
                schema: "org");

            migrationBuilder.DropTable(
                name: "permission",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "app_user",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "role",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "tenant",
                schema: "org");
        }
    }
}
