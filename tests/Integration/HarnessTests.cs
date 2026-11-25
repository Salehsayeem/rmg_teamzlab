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

        // Seeded admin verifies against the default seed password.
        var admin = await Db.Set<AppUser>().SingleAsync(u => u.Email == "admin@tzl.demo");
        var hasher = new PasswordHasher<AppUser>();
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(admin, admin.PasswordHash!, "Admin#12345"));
    }
}
