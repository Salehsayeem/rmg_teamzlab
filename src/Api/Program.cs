using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rmg.Api;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Outbox;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Database;
using Rmg.Modules.Identity.Infrastructure;
using Rmg.Modules.Merchandising.Infrastructure;
using Rmg.Modules.Organization.Infrastructure;
using Rmg.Modules.ProductEngineering.Infrastructure;

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

// ── R-2: JWT auth, permission policies, tenant context, login rate limit (Task 4) ──────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured (set env Jwt__Key, ≥32 chars).");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

builder.Services.AddDbContext<RmgDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<IJwtIssuer>(new JwtIssuer(jwtKey));
builder.Services.AddScoped<IAuthUserStore, AuthUserStore>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, _) => await ProblemJson.WriteAsync(
        ctx.HttpContext, StatusCodes.Status429TooManyRequests, "RATE_LIMITED", "Too Many Requests");
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep claim types verbatim: sub/tid/fid/perm
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = JwtIssuer.TokenIssuer,
            ValidAudience = JwtIssuer.TokenAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                await ProblemJson.WriteAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized, "UNAUTHENTICATED", "Unauthorized");
            },
            OnForbidden = ctx => ProblemJson.WriteAsync(ctx.HttpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "Forbidden"),
        };
    });
builder.Services.AddAuthorization();
// ───────────────────────────────────────────────────────────────────────────────────────────

// BEGIN T6 (additive block): audit + outbox writers resolve the request's DbContext/tenant context.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
// END T6

// ── Task 7 (guard): tenant-scoped context carries the TenantEntity query filter for the guard
// probes; the [Idempotent] filter stores/replays via the context's base type (the Idempotency project
// cannot see the Database project), forwarded to the same scoped instance. ──
builder.Services.AddDbContext<TenantScopedDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
// writers, probes and the [Idempotent] filter resolve the ambient context by its base type —
// forwarded to the same scoped, tenant-filtered instance (one SaveChanges = atomic business+outbox).
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TenantScopedDbContext>());

// ── W1 merge: module DI composition roots — controllers receive ctor-injected module services
// (replaces FacilityConfig.CreateApi and IamController's per-request property getters). ──
builder.Services.AddOrganizationApi();
builder.Services.AddIamApi();

// BEGIN Task 10 (ORD): buyer module DI composition (MerchandisingConfig).
builder.Services.AddMerchandisingApi();
// END Task 10 (ORD)

// BEGIN Task 11 (PDM): material master + uom — controllers receive ctor-injected module services
// (mirrors the W1 module composition blocks above).
builder.Services.AddPdmApi();
// END Task 11

// ── Controller seam (controller-authored, P-12): module endpoints are MVC controllers in
// src/Api/Controllers (plan convention) — discovered from the entry assembly. ──
builder.Services.AddControllers();

// BEGIN Task 15 (contract): OpenAPI export — AddEndpointsApiExplorer surfaces the minimal-API login
// endpoint through ApiExplorer alongside the MVC controllers, so OpenApiExport.cs can generate the
// whole pilot document (the pinned stack carries no Microsoft.AspNetCore.OpenApi package; the
// generator walks ApiExplorer instead). GET /openapi/v1.json is mapped Development-only after
// MapControllers; scripts/export-openapi.ps1 snapshots the document into
// contracts/openapi/openapi.json, and tests/Contract asserts the live document matches it.
builder.Services.AddEndpointsApiExplorer();
// END Task 15

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => "Hello World!");
app.MapDevProbeEndpoints();
app.MapAuthEndpoints();
app.MapAuthProbeEndpoints();
// BEGIN Task 14 (ORD): [Idempotent] confirm — an endpoint filter reaches MVC actions only through
// the MapControllers convention builder (docs: "Register a filter on controller actions"); the
// filter self-gates on the [Idempotent] metadata, so nothing else changes behavior.
app.MapControllers().AddEndpointFilter<Microsoft.AspNetCore.Builder.ControllerActionEndpointConventionBuilder,
    Rmg.BuildingBlocks.Idempotency.IdempotencyFilter>();
// END Task 14 (ORD)

// BEGIN Task 15 (contract): Development-only OpenAPI document (see the Task 15 block at AddControllers).
if (app.Environment.IsDevelopment())
    app.MapOpenApiExport();
// END Task 15

// ── Task 7 (guard): TenantEntity's query filter reads the ambient tenant — mirror it from
// ITenantContext once TenantResolutionMiddleware has populated it — plus the guard probe endpoints.
// Keyed POST bodies are buffered here because the minimal-API pipeline drains the request body
// before [Idempotent] filters run; the filter hashes the captured bytes. ──
app.Use(async (context, next) =>
{
    TenantEntity.SetAmbientTenant(context.RequestServices.GetRequiredService<ITenantContext>().TenantId);

    if (HttpMethods.IsPost(context.Request.Method)
        && context.Request.Headers.ContainsKey("Idempotency-Key"))
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        context.Items[Rmg.BuildingBlocks.Idempotency.IdempotencyFilter.RequestBodyItemKey] = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
    }

    await next(context);
});
app.MapGuardProbeEndpoints();

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
        // BEGIN Task 14 (ORD): spec §8 demo dataset — browser-ready buyer/style/BOM/order after boot.
        // Skipped on a database that has not migrated to 005 yet (e.g. a dev DB awaiting `db reset`);
        // the reset path always migrates first, so the demo dataset installs there.
        if ((await db.Database.GetAppliedMigrationsAsync()).Any(m => m.EndsWith("005_OrdOrder")))
            await Seeder.DemoSeedAsync(db);
        // END Task 14 (ORD)
    }
}

app.Run();

public partial class Program;
