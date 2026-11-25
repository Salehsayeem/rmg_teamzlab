using Npgsql;

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
}
