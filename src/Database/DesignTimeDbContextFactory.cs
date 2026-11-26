using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rmg.Database;

// Lets `dotnet ef migrations add` build the context outside a host (Api does not register one in DI).
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RmgDbContext>
{
    public RmgDbContext CreateDbContext(string[] args)
    {
        if (Environment.GetEnvironmentVariable("ConnectionStrings__Default") is null)
        {
            var envFile = FindEnvFile();
            if (envFile is not null) DotNetEnv.Env.Load(envFile);
        }
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default is missing (set it or provide a repo-root .env).");

        return new RmgDbContext(new DbContextOptionsBuilder<RmgDbContext>()
            .UseNpgsql(connectionString)
            .Options);
    }

    // dotnet-ef runs with cwd = project dir, so walk up to the repo root like the test harness does.
    private static string? FindEnvFile()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".env")))
            dir = dir.Parent;
        return dir is null ? null : Path.Combine(dir.FullName, ".env");
    }
}
