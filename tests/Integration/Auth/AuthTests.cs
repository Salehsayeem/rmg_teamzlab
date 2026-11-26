using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Common;
using Rmg.Database;
using Rmg.Modules.Identity.Domain;
using Rmg.Modules.Organization.Domain;

namespace Rmg.IntegrationTests;

// R-2 acceptance: login issues a verifiable JWT, and every auth failure is problem+json with a stable code.
public class AuthTests : IntegrationTestBase
{
    // The Api's Development boot hook and/or Seeder below create admin@tzl.demo; both hash with this.
    private static string AdminPassword =>
        Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? Seeder.DefaultAdminPassword;

    private async Task SeedAsync()
    {
        // Idempotent pilot seed (tenant, catalogue, roles, admin with every permission).
        await Seeder.SeedAsync(Db);
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginEnvelope>();
        return body!.Data.AccessToken;
    }

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsToken()
    {
        await SeedAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@tzl.demo", password = AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginEnvelope>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Data.AccessToken));
        Assert.Equal(28800, body.Data.ExpiresInSeconds);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.User.DisplayName));

        // The issued token actually verifies: it opens a [RequirePerm] endpoint.
        Client.DefaultRequestHeaders.Authorization = new("Bearer", body.Data.AccessToken);
        var probe = await Client.GetAsync("/authz/probe/perm");
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_401_INVALID_CREDENTIALS()
    {
        await SeedAsync();

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@tzl.demo", password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("INVALID_CREDENTIALS", problem!.Code);
    }

    [Fact]
    public async Task Protected_WithoutToken_401()
    {
        var response = await Client.GetAsync("/authz/probe/perm");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("UNAUTHENTICATED", problem!.Code);
    }

    [Fact]
    public async Task RequirePerm_MissingPerm_403_PERMISSION_DENIED()
    {
        await SeedAsync();

        // A user whose role carries no permissions.
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var hasher = new PasswordHasher<AppUser>();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "limited@tzl.demo",
            DisplayName = "Limited",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        user.PasswordHash = hasher.HashPassword(user, AdminPassword);
        var role = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Code = "Limited", Revision = 1, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "test" };
        Db.AddRange(user, role, new UserRole { TenantId = tenantId, UserId = user.Id, RoleId = role.Id });
        await Db.SaveChangesAsync();

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<LoginEnvelope>();
        Client.DefaultRequestHeaders.Authorization = new("Bearer", body!.Data.AccessToken);

        var response = await Client.GetAsync("/authz/probe/perm");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("PERMISSION_DENIED", problem!.Code);
    }

    [Fact]
    public async Task Claims_PopulateTenantContext()
    {
        await SeedAsync();

        var admin = await Db.Set<AppUser>().SingleAsync(u => u.Email == "admin@tzl.demo");
        var token = await LoginAsync("admin@tzl.demo");
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await Client.GetAsync("/authz/probe/tenant");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(admin.TenantId.ToString(), data.GetProperty("tenantId").GetString());
        Assert.Equal(admin.Id.ToString(), data.GetProperty("userId").GetString());
        // No user→facility mapping exists in M001, so fid is legitimately null.
        Assert.Equal(JsonValueKind.Null, data.GetProperty("facilityId").ValueKind);
    }

    [Fact]
    public async Task Login_RateLimited_TenPerMinutePerIp()
    {
        await SeedAsync();

        // Ten passes through the fixed window; the 11th is rejected before the endpoint runs.
        for (var i = 0; i < 10; i++)
            await Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@tzl.demo", password = "x" });

        var rejected = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@tzl.demo", password = "x" });

        Assert.Equal((int)HttpStatusCode.TooManyRequests, (int)rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("RATE_LIMITED", problem!.Code);
    }

    // ── response body shapes ────────────────────────────────────────────
    private sealed record LoginEnvelope(LoginData Data);
    private sealed record LoginData(string AccessToken, int ExpiresInSeconds, LoginUserInfo User);
    private sealed record LoginUserInfo(Guid Id, string DisplayName);
    private sealed record ProblemBody(string Code);
}
