using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rmg.Modules.Identity.Domain;
using Rmg.Modules.Organization.Domain;

namespace Rmg.Database;

// Pilot seed: demo tenant + org structure, IAM catalogue, roles, admin login. Idempotent — no-op
// when the seed tenant already exists. Used by the db-reset CLI and the Api Development boot hook.
public static class Seeder
{
    public const string DefaultAdminPassword = "Admin#12345";

    private const string TenantCode = "TZL";

    // Spec §5 catalogue — module.resource.action.
    private static readonly (string Code, string Description)[] Permissions =
    [
        ("org.read", "View organization data"),
        ("iam.user.read", "View users"),
        ("iam.user.manage", "Manage users and role assignments"),
        ("pdm.material.read", "View materials"),
        ("pdm.material.create", "Create materials"),
        ("pdm.style.read", "View styles"),
        ("pdm.style.create", "Create styles"),
        ("pdm.style.edit", "Edit styles"),
        ("pdm.style.approve", "Approve styles"),
        ("pdm.bom.read", "View BOMs"),
        ("pdm.bom.create", "Create BOMs"),
        ("pdm.bom.edit", "Edit BOMs"),
        ("pdm.bom.approve", "Approve (release) BOMs"),
        ("ord.buyer.read", "View buyers"),
        ("ord.buyer.create", "Create buyers"),
        ("ord.buyer.manage", "Manage buyer profiles"),
        ("ord.order.read", "View customer orders"),
        ("ord.order.create", "Create customer orders"),
        ("ord.order.edit", "Edit customer orders"),
        ("ord.order.confirm", "Confirm customer orders"),
        ("audit.read", "View audit trail"),
    ];

    public static async Task SeedAsync(RmgDbContext db, string adminPassword = DefaultAdminPassword)
    {
        if (await db.Set<Tenant>().AnyAsync(t => t.Code == TenantCode))
            return;

        var now = DateTime.UtcNow;
        var by = "seed";

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "TeamzLab Demo Ltd.",
            Code = TenantCode,
            DefaultLocale = "en",
            RequireMakerChecker = false,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        db.Add(tenant);
        db.AddRange(
            new TenantLocalePolicy { Id = Guid.NewGuid(), TenantId = tenant.Id, Locale = "en", IsDefault = true, Revision = 1, CreatedAtUtc = now, CreatedBy = by },
            new TenantLocalePolicy { Id = Guid.NewGuid(), TenantId = tenant.Id, Locale = "bn", IsDefault = false, Revision = 1, CreatedAtUtc = now, CreatedBy = by });

        var legalEntity = new LegalEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Code = "TZL-HQ",
            Name = "TeamzLab Demo Ltd.",
            Country = "BD",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        db.Add(legalEntity);
        db.Add(new Facility
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            LegalEntityId = legalEntity.Id,
            Code = "FAC-01",
            Name = "TeamzLab Demo Facility",
            Timezone = "Asia/Dhaka",
            Address = null,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        });

        var permissions = Permissions.Select(p => new Permission { Code = p.Code, Description = p.Description });
        db.AddRange(permissions);

        var roles = new[] { "Admin", "Merchandiser", "Approver" }
            .Select(code => new Role { Id = Guid.NewGuid(), TenantId = tenant.Id, Code = code, Revision = 1, CreatedAtUtc = now, CreatedBy = by })
            .ToList();
        db.AddRange(roles);

        // Admin gets the full catalogue; Merchandiser/Approver grants are wired by the IAM task.
        var adminRole = roles.Single(r => r.Code == "Admin");
        db.AddRange(Permissions.Select(p => new RolePermission { RoleId = adminRole.Id, PermissionCode = p.Code }));

        var hasher = new PasswordHasher<AppUser>();
        var admin = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "admin@tzl.demo",
            DisplayName = "Admin",
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
        db.Add(admin);
        db.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync();
    }
}
