using Microsoft.EntityFrameworkCore;

namespace Rmg.Database;

// Minimal db CLI: `dotnet run --project src/Database -- reset`. Named class (not top-level
// statements) so the global-namespace `Program` doesn't collide with Api's (CS0433, same as Workers).
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is not ["reset"])
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/Database -- reset");
            return 1;
        }

        // Walked up from cwd because dotnet run doesn't guarantee cwd = repo root.
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, ".env"))) continue;
            DotNetEnv.Env.Load(Path.Combine(dir.FullName, ".env"));
            break;
        }
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default is missing (set it or provide a repo-root .env).");
        var adminPassword = Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? Seeder.DefaultAdminPassword;

        Console.WriteLine("Dropping pilot schemas...");
        await DbReset.ResetAsync(connectionString);

        Console.WriteLine("Migrating and seeding...");
        await using var db = new RmgDbContext(new DbContextOptionsBuilder<RmgDbContext>()
            .UseNpgsql(connectionString)
            .Options);
        await Seeder.SeedAsync(db, adminPassword);
        await Seeder.DemoSeedAsync(db); // Task 14: spec §8 demo dataset (buyer/style/BOM/order)

        Console.WriteLine("Database reset complete.");
        return 0;
    }
}
