using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rmg.Api;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
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
