using Microsoft.EntityFrameworkCore;
using Rmg.Api;
using Rmg.BuildingBlocks.Common;
using Rmg.Database;

// Local config comes from the gitignored .env (connection string, JWT key, seed password, ports).
// Walked up from cwd because dotnet run / IIS Express don't guarantee cwd = repo root.
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
{
    if (!File.Exists(Path.Combine(dir.FullName, ".env"))) continue;
    DotNetEnv.Env.Load(Path.Combine(dir.FullName, ".env"));
    break;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationMiddleware>();

app.MapGet("/", () => "Hello World!");
app.MapDevProbeEndpoints();

if (app.Environment.IsDevelopment())
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (!string.IsNullOrEmpty(connectionString))
    {
        // Dev convenience: make sure the pilot seed exists on boot (idempotent); run
        // `dotnet run --project src/Database -- reset` for a fresh schema first.
        await using var db = new RmgDbContext(new DbContextOptionsBuilder<RmgDbContext>()
            .UseNpgsql(connectionString)
            .Options);
        await Seeder.SeedAsync(db, builder.Configuration["Seed:AdminPassword"] ?? Seeder.DefaultAdminPassword);
    }
}

app.Run();

public partial class Program;
