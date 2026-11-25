using Microsoft.EntityFrameworkCore;
using Rmg.Api;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Outbox;
using Rmg.BuildingBlocks.Tenancy;
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

// BEGIN T6 (additive block): audit + outbox writers resolve the request's DbContext/tenant context.
builder.Services.AddDbContext<RmgDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.")));
// writers take the ambient DbContext (base type) — forward to the same scoped RmgDbContext instance
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<RmgDbContext>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
// END T6

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
