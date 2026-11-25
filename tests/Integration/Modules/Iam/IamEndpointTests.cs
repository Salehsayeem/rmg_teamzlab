using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rmg.Database;
using Rmg.Modules.Identity.Domain;
using Rmg.Modules.Organization.Domain;

namespace Rmg.IntegrationTests;

// Task 9 IAM endpoints: user creation hashes with PasswordHasher and assigns tenant roles, duplicate
// email is 422 EMAIL_TAKEN, password hashes never leave the API, writes require iam.user.manage while
// reads need only iam.user.read, and the users list is tenant-scoped (the AppUser global filter,
// context-rooted in TenantScopedDbContext — the base login context stays deliberately unfiltered).
public class IamEndpointTests : IntegrationTestBase
{
    private static string AdminPassword =>
        Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? Seeder.DefaultAdminPassword;

    private async Task<string> AdminTokenAsync()
    {
        await Seeder.SeedAsync(Db);
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@tzl.demo", password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private async Task<JsonDocument> PostCreateUserAsync(string token, string email, string[] roleCodes)
    {
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await Client.PostAsJsonAsync("/api/v1/iam/users",
            new { email, displayName = "Mari", password = "Mari#12345", roleCodes });
        Assert.True(response.IsSuccessStatusCode, $"create failed: {(int)response.StatusCode}");
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    [Fact]
    public async Task CreateUser_AssignsRoles_LoginWorks()
    {
        var token = await AdminTokenAsync();

        var created = await PostCreateUserAsync(token, "mari@tzl.demo", ["Merchandiser"]);
        var data = created.RootElement.GetProperty("data");
        var userId = data.GetProperty("id").GetGuid();
        Assert.Equal("mari@tzl.demo", data.GetProperty("email").GetString());
        Assert.Equal("Mari", data.GetProperty("displayName").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.Equal(JsonValueKind.Array, data.GetProperty("roles").ValueKind);
        Assert.Equal(["Merchandiser"],
            data.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());

        // Same-tenant user_role links persisted (tenant_id inside both composite FKs).
        var links = await Db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync();
        var merchandiser = await Db.Set<Role>().SingleAsync(r => r.Code == "Merchandiser");
        var link = Assert.Single(links);
        Assert.Equal(merchandiser.Id, link.RoleId);

        // The created credentials really log in — the hash came from PasswordHasher.
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "mari@tzl.demo", password = "Mari#12345" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_422_EMAIL_TAKEN()
    {
        var token = await AdminTokenAsync();

        await PostCreateUserAsync(token, "mari@tzl.demo", ["Merchandiser"]);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var dupe = await Client.PostAsJsonAsync("/api/v1/iam/users",
            new { email = "mari@tzl.demo", displayName = "Mari II", password = "Mari#12345", roleCodes = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, dupe.StatusCode);
        var problem = await JsonDocument.ParseAsync(await dupe.Content.ReadAsStreamAsync());
        Assert.Equal("EMAIL_TAKEN", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(422, problem.RootElement.GetProperty("status").GetInt32());

        // The seeded admin's email is equally taken.
        var seededDupe = await Client.PostAsJsonAsync("/api/v1/iam/users",
            new { email = "admin@tzl.demo", displayName = "X", password = "X#12345", roleCodes = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, seededDupe.StatusCode);
    }

    [Fact]
    public async Task Users_List_NeverReturnsHash()
    {
        var token = await AdminTokenAsync();
        await PostCreateUserAsync(token, "mari@tzl.demo", ["Merchandiser"]);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var list = await Client.GetAsync("/api/v1/iam/users");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var raw = await list.Content.ReadAsStringAsync();
        Assert.Contains("mari@tzl.demo", raw);
        Assert.Contains("admin@tzl.demo", raw);
        // No hash under any casing/column name ever crosses the wire.
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUser_WithoutManagePerm_403()
    {
        var token = await AdminTokenAsync();

        // A reader: iam.user.read only, no iam.user.manage.
        var tenantId = await Db.Set<Tenant>().Select(t => t.Id).SingleAsync();
        var hasher = new PasswordHasher<AppUser>();
        var reader = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "reader@tzl.demo",
            DisplayName = "Reader",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        reader.PasswordHash = hasher.HashPassword(reader, AdminPassword);
        var readRole = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Code = "Reader", Revision = 1, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "test" };
        Db.AddRange(reader, readRole,
            new RolePermission { RoleId = readRole.Id, PermissionCode = "iam.user.read" },
            new UserRole { TenantId = tenantId, UserId = reader.Id, RoleId = readRole.Id });
        await Db.SaveChangesAsync();

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = reader.Email, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        Client.DefaultRequestHeaders.Authorization =
            new("Bearer", body.RootElement.GetProperty("data").GetProperty("accessToken").GetString());

        // Reads pass with iam.user.read (control: the 403 below is about manage, not the route).
        var read = await Client.GetAsync("/api/v1/iam/roles");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var create = await Client.PostAsJsonAsync("/api/v1/iam/users",
            new { email = "blocked@tzl.demo", displayName = "B", password = "B#12345", roleCodes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        var problem = await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync());
        Assert.Equal("PERMISSION_DENIED", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Users_List_OtherTenant_Invisible()
    {
        var token = await AdminTokenAsync();

        // Another tenant's user, inserted directly — the users list must never surface it.
        var other = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Other Ltd.",
            Code = "OTH",
            DefaultLocale = "en",
            RequireMakerChecker = false,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        var otherUser = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = other.Id,
            Email = "rival@oth.demo",
            DisplayName = "Rival",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test",
        };
        otherUser.PasswordHash = new PasswordHasher<AppUser>().HashPassword(otherUser, AdminPassword);
        Db.AddRange(other, otherUser);
        await Db.SaveChangesAsync();

        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var raw = await (await Client.GetAsync("/api/v1/iam/users")).Content.ReadAsStringAsync();

        Assert.Contains("admin@tzl.demo", raw);
        Assert.DoesNotContain("rival@oth.demo", raw);
    }

    [Fact]
    public async Task SetRoles_ReplacesRoleSet_AndValidatesCodes()
    {
        var token = await AdminTokenAsync();
        var created = await PostCreateUserAsync(token, "mari@tzl.demo", ["Merchandiser"]);
        var userId = created.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Replace: Merchandiser out, Approver+Admin in.
        var replace = await Client.PostAsJsonAsync($"/api/v1/iam/users/{userId}/roles",
            new { roleCodes = new[] { "Approver", "Admin" } });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var data = (await JsonDocument.ParseAsync(await replace.Content.ReadAsStreamAsync())).RootElement.GetProperty("data");
        Assert.Equal(["Admin", "Approver"],
            data.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).OrderBy(r => r).ToArray());
        var persisted = (await Db.Set<UserRole>().Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync())
            .ToHashSet();
        var expected = await Db.Set<Role>().Where(r => r.Code == "Approver" || r.Code == "Admin").Select(r => r.Id).ToListAsync();
        Assert.Equal(expected.ToHashSet(), persisted);

        // Unknown role code → 422 ROLE_INVALID, nothing changed.
        var bad = await Client.PostAsJsonAsync($"/api/v1/iam/users/{userId}/roles",
            new { roleCodes = new[] { "NoSuchRole" } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        var problem = await JsonDocument.ParseAsync(await bad.Content.ReadAsStreamAsync());
        Assert.Equal("ROLE_INVALID", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, await Db.Set<UserRole>().Where(ur => ur.UserId == userId).CountAsync());

        // Unknown user id → 404, never a leak.
        var missing = await Client.PostAsJsonAsync($"/api/v1/iam/users/{Guid.NewGuid()}/roles",
            new { roleCodes = new[] { "Admin" } });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
