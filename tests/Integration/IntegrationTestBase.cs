using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Rmg.Database;

namespace Rmg.IntegrationTests;

// Base for DB-dependent tests: each class gets a scratch database (migrated) plus an HttpClient to Rmg.Api wired to it.
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private ScratchDb _db = null!;
    private WebApplicationFactory<Program> _factory = null!;

    protected string ConnectionString { get; private set; } = null!;
    protected RmgDbContext Db { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _db = await ScratchDb.CreateAsync(AdminConnectionString());
        ConnectionString = _db.ConnectionString;
        try
        {
            await DbBootstrapper.MigrateAsync(ConnectionString);
            Db = new RmgDbContext(new DbContextOptionsBuilder<RmgDbContext>()
                .UseNpgsql(ConnectionString)
                .Options);
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", ConnectionString));
            Client = _factory.CreateClient();
        }
        catch
        {
            await TeardownAsync();
            throw;
        }
    }

    public Task DisposeAsync() => TeardownAsync();

    // Best-effort, null-safe: every resource is disposed even if an earlier one throws (a WAF whose hosted app
    // faulted throws on Dispose), so the scratch-DB drop always runs and cannot be masked by an NRE.
    private async Task TeardownAsync()
    {
        if (Db is not null)
            try { await Db.DisposeAsync(); }
            catch { /* secondary teardown failure */ }
        if (_factory is not null)
            try { await _factory.DisposeAsync(); }
            catch { /* secondary teardown failure */ }
        if (_db is not null)
            await _db.DisposeAsync();
    }

    // Admin connection string from env or the repo-root .env (DotNetEnv sets it into the process environment).
    internal static string AdminConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (!string.IsNullOrEmpty(cs)) return cs;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".env"))) dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException(
                "No admin connection string: set ConnectionStrings__Default or a repo-root .env containing it.");

        DotNetEnv.Env.Load(Path.Combine(dir.FullName, ".env"));
        return Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default missing from .env.");
    }
}
