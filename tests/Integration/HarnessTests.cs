using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rmg.Database;
using Rmg.Modules.Identity.Domain;

namespace Rmg.IntegrationTests;

// Harness acceptance: bootstrap on a fresh scratch database produces the module schemas.
public class HarnessTests : IntegrationTestBase
{
    [Fact]
    public async Task DbMigrationsApply_CreateSchemas()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var schemas = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT schema_name FROM information_schema.schemata", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                schemas.Add(reader.GetString(0));

        foreach (var schema in new[] { "org", "iam", "pdm", "ord", "outbox", "audit" })
            Assert.Contains(schema, schemas);
    }

    // M001 acceptance: bootstrap + seed produces all foundation tables and a working admin login.
    [Fact]
    public async Task M001_CreatesAllTables_AndSeedProducesAdminLogin()
    {
        await Seeder.SeedAsync(Db);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // All M001 tables exist in their pilot schemas.
        var tables = new HashSet<string>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_schema IN ('org','iam','outbox','audit')", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        foreach (var table in new[]
                 {
                     "org.tenant", "org.legal_entity", "org.facility", "org.tenant_locale_policy",
                     "iam.app_user", "iam.role", "iam.permission", "iam.role_permission", "iam.user_role",
                     "iam.idempotency_record", "outbox.outbox_message", "audit.audit_event",
                 })
            Assert.Contains(table, tables);

        // Permission catalogue fully seeded.
        await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM iam.permission", conn))
            Assert.Equal(21L, (long)(await cmd.ExecuteScalarAsync())!);

        // R-6 cross-tenant guard: user_role carries tenant_id NOT NULL and both its FKs are
        // composite, leading with tenant_id (user and role must belong to the same tenant).
        await using (var cmd = new NpgsqlCommand(
            "SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'iam' AND table_name = 'user_role' AND column_name = 'tenant_id'", conn))
            Assert.Equal("NO", await cmd.ExecuteScalarAsync() as string);
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT count(DISTINCT tc.constraint_name) AS fk_count,
                   count(*) FILTER (WHERE kcu.ordinal_position = 1 AND kcu.column_name = 'tenant_id') AS tenant_leading
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.constraint_schema = kcu.constraint_schema
            WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'iam' AND tc.table_name = 'user_role'
            """, conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(2L, reader.GetInt64(0));
            Assert.Equal(2L, reader.GetInt64(1));
        }

        // Admin role has the full catalogue.
        await using (var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM iam.role_permission rp JOIN iam.role r ON r.id = rp.role_id WHERE r.code = 'Admin'", conn))
            Assert.Equal(21L, (long)(await cmd.ExecuteScalarAsync())!);

        // Seeded admin verifies against the default seed password.
        var admin = await Db.Set<AppUser>().SingleAsync(u => u.Email == "admin@tzl.demo");
        var hasher = new PasswordHasher<AppUser>();
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(admin, admin.PasswordHash!, "Admin#12345"));
    }
}
