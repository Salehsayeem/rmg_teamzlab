using Microsoft.EntityFrameworkCore;

namespace Rmg.Database;

// Applies migrations and guarantees the module schemas exist (harness bootstrap; later CLI reset reuses this).
public static class DbBootstrapper
{
    // Organization, identity, product engineering, orders, outbox, audit.
    internal static readonly string[] ModuleSchemas = ["org", "iam", "pdm", "ord", "outbox", "audit"];

    public static async Task MigrateAsync(string connectionString)
    {
        await using var db = new RmgDbContext(new DbContextOptionsBuilder<RmgDbContext>()
            .UseNpgsql(connectionString)
            .Options);

        // Identifiers come from the internal whitelist below, never user input — plain SQL is the only way to
        // create schemas (identifiers cannot be sent as bind parameters).
        var sql = string.Join("; ", ModuleSchemas.Select(s => $"CREATE SCHEMA IF NOT EXISTS {s}"));
        await db.Database.ExecuteSqlRawAsync(sql);

        await db.Database.MigrateAsync();
    }
}
