using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rmg.Database;
using Rmg.Modules.Identity.Domain;
using Rmg.Modules.Organization.Domain;

namespace Rmg.IntegrationTests;

// Two-tenant fixture for the tenant-guard and idempotency suites. The Api Development boot hook seeds
// the pilot TZL tenant (admin@tzl.demo with the full catalogue, facility FAC-01) before the test body
// runs; this adds tenant TNB beside it plus the probe-only write permission both admin roles need.
public static class GuardTestSeed
{
    public const string ProbeWritePerm = "org.facility.write";

    public static string Password =>
        Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? Seeder.DefaultAdminPassword;

    public sealed record GuardFixture(
        Guid TenantA, Guid LegalEntityA, Guid FacilityA1, Guid FacilityA2, string AdminAEmail,
        Guid TenantB, Guid LegalEntityB, Guid FacilityB1, string AdminBEmail);

    public static async Task<GuardFixture> SeedAsync(RmgDbContext db)
    {
        var now = DateTime.UtcNow;
        var by = "guard-test";

        // Tenant A: the pilot TZL seed (FAC-01 + admin@tzl.demo holding the full catalogue).
        var tenantA = await db.Set<Tenant>().SingleAsync(t => t.Code == "TZL");
        var legalEntityA = await db.Set<LegalEntity>().SingleAsync(x => x.TenantId == tenantA.Id);
        var facilityA1 = await db.Set<Facility>().IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantA.Id).Select(f => f.Id).SingleAsync();
        var facilityA2 = NewFacility(tenantA.Id, legalEntityA.Id, "FAC-A2", now, by);
        var adminA = await db.Set<AppUser>().SingleAsync(u => u.Email == "admin@tzl.demo");
        var roleA = await db.Set<Role>().SingleAsync(r => r.TenantId == tenantA.Id && r.Code == "Admin");

        // Tenant B: a complete second stack of the same shape.
        var tenantB = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Tenant B Ltd.",
            Code = "TNB",
            DefaultLocale = "en",
            RequireMakerChecker = false,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        var legalEntityB = new LegalEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB.Id,
            Code = "TNB-HQ",
            Name = "Tenant B Ltd.",
            Country = "BD",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        var facilityB1 = NewFacility(tenantB.Id, legalEntityB.Id, "FAC-B1", now, by);
        var roleB = new Role { Id = Guid.NewGuid(), TenantId = tenantB.Id, Code = "Admin", Revision = 1, CreatedAtUtc = now, CreatedBy = by };
        var adminB = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB.Id,
            Email = "admin@tnb.demo",
            DisplayName = "Admin B",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        adminB.PasswordHash = new PasswordHasher<AppUser>().HashPassword(adminB, Password);

        db.AddRange(tenantB, legalEntityB, facilityA2, facilityB1, roleB, adminB,
            new UserRole { TenantId = tenantB.Id, UserId = adminB.Id, RoleId = roleB.Id },
            new Permission { Code = ProbeWritePerm, Description = "Probe-only write permission (tenant guard tests)" },
            new RolePermission { RoleId = roleA.Id, PermissionCode = ProbeWritePerm },
            new RolePermission { RoleId = roleB.Id, PermissionCode = "org.read" },
            new RolePermission { RoleId = roleB.Id, PermissionCode = ProbeWritePerm });
        await db.SaveChangesAsync();

        return new GuardFixture(tenantA.Id, legalEntityA.Id, facilityA1, facilityA2.Id, adminA.Email,
            tenantB.Id, legalEntityB.Id, facilityB1.Id, adminB.Email);
    }

    private static Facility NewFacility(Guid tenantId, Guid legalEntityId, string code, DateTime now, string by) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        LegalEntityId = legalEntityId,
        Code = code,
        Name = $"Facility {code}",
        Timezone = "Asia/Dhaka",
        Revision = 1,
        CreatedAtUtc = now,
        CreatedBy = by,
    };

    public static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    public static async Task<int> CountFacilitiesAsync(RmgDbContext db, Guid tenantId) =>
        await db.Set<Facility>().IgnoreQueryFilters().CountAsync(f => f.TenantId == tenantId);
}
