using Microsoft.EntityFrameworkCore;

namespace Rmg.Database;

// Full local reset: drop the pilot schemas + migration history, re-apply migrations (forward-only)
// from scratch, so `reset` always yields exactly the current schema.
public static class DbReset
{
    public static async Task ResetAsync(string connectionString)
    {
        await using var db = new RmgDbContext(new DbContextOptionsBuilder<RmgDbContext>()
            .UseNpgsql(connectionString)
            .Options);

        // Identifiers come from the internal schema whitelist, never user input. The history table is
        // quoted: it is mixed-case, so the unquoted form folds to lowercase and silently matches nothing.
        var sql = string.Join("; ", DbBootstrapper.ModuleSchemas.Select(s => $"DROP SCHEMA IF EXISTS {s} CASCADE"))
            + "; DROP TABLE IF EXISTS public.\"__EFMigrationsHistory\"";
        await db.Database.ExecuteSqlRawAsync(sql);

        await DbBootstrapper.MigrateAsync(connectionString);
    }
}
