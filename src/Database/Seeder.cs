using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rmg.Modules.Identity.Domain;
using Rmg.Modules.Merchandising.Domain;
using Rmg.Modules.Organization.Domain;
using Rmg.Modules.ProductEngineering.Domain;

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
        db.Add(new UserRole { TenantId = tenant.Id, UserId = admin.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync();
    }

    // Spec §8 (FND-028) demo dataset, layered on SeedAsync's tenant/iam rows: buyer BUY-001 with a
    // contact, the fabric/thread/button material set, style STY-001 Approved with a 3 colors × 4
    // sizes variant matrix, its released BOM, and order ORD-001 Confirmed. No extra users (the
    // seeded admin holds the full catalogue — same password source as SeedAsync) and no outbox/audit
    // rows (seed data, not commands). Idempotent — no-op when ORD-001 exists. Runs on the base
    // context, whose fail-closed tenant filter hides everything: reads use IgnoreQueryFilters().
    public static async Task DemoSeedAsync(RmgDbContext db)
    {
        if (await db.Set<CustomerOrder>().IgnoreQueryFilters().AnyAsync(o => o.Code == "ORD-001"))
            return;

        var now = DateTime.UtcNow;
        var by = "seed";
        var tenantId = await db.Set<Tenant>().Select(t => t.Id).SingleAsync();

        // Buyer BUY-001 + contact (spec §8: 1 buyer with contact).
        var buyer = new Buyer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "BUY-001",
            Name = "Acme Apparel Ltd.",
            Country = "BD",
            Currency = "USD",
            Status = "Active",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        var contact = new BuyerContact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BuyerId = buyer.Id,
            Name = "Nadia Islam",
            Email = "nadia@acme.demo",
            Phone = "+8801700000000",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };

        // Material set: fabric + thread + button (uom codes from the 003_PdmMaster catalogue).
        var fabric = new Material
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "RM-FAB-01",
            Name = "Cotton single jersey 180 gsm",
            Category = "Fabric",
            BaseUomCode = "KG",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        var thread = new Material
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "RM-THR-01",
            Name = "Polyester spun thread 120",
            Category = "Trims",
            BaseUomCode = "KG",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        var button = new Material
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "RM-BTN-01",
            Name = "2-hole button 15 mm",
            Category = "Trims",
            BaseUomCode = "PC",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };

        // Style STY-001 Approved, version 1, 3 colors × 4 sizes variant matrix.
        var style = new Style
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "STY-001",
            Name = "Classic Polo Tee",
            Season = "SS26",
            Category = "Polo",
            BuyerId = buyer.Id,
            Status = "Approved",
            Revision = 2,
            CreatedAtUtc = now,
            CreatedBy = by,
            UpdatedAtUtc = now,
            UpdatedBy = by,
        };
        var version = new StyleVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StyleId = style.Id,
            Version = 1,
            Status = "Approved",
            ApprovedAtUtc = now,
            Revision = 2,
            CreatedAtUtc = now,
            CreatedBy = by,
            UpdatedAtUtc = now,
            UpdatedBy = by,
        };
        string[] colors = ["NAVY", "WHITE", "BLACK"];
        string[] sizes = ["S", "M", "L", "XL"];
        var variants = colors.SelectMany(c => sizes.Select(s => new StyleVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StyleVersionId = version.Id,
            ColorCode = c,
            SizeCode = s,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        })).ToArray();

        // Released BOM (3 lines) pinned to the Approved version.
        var bom = new Bom
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StyleVersionId = version.Id,
            Status = "Released",
            Revision = 2,
            CreatedAtUtc = now,
            CreatedBy = by,
            UpdatedAtUtc = now,
            UpdatedBy = by,
        };
        var bomLines = new[]
        {
            (fabric.Id, 0.32m, "KG", 5m), (thread.Id, 0.02m, "KG", 5m), (button.Id, 12m, "PC", 0m),
        }.Select(x => new BomLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BomId = bom.Id,
            MaterialId = x.Item1,
            QtyPerUnit = x.Item2,
            UomCode = x.Item3,
            WastePct = x.Item4,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        }).ToArray();

        // Order ORD-001 Confirmed — one line over the whole matrix, line_qty = Σ sizes = 14400.
        var order = new CustomerOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "ORD-001",
            BuyerId = buyer.Id,
            BuyerPo = "PO-2026-0001",
            Currency = "USD",
            OrderDate = new DateOnly(2026, 1, 15),
            Status = "Confirmed",
            Revision = 2,
            CreatedAtUtc = now,
            CreatedBy = by,
            UpdatedAtUtc = now,
            UpdatedBy = by,
        };
        var orderLine = new OrderLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order.Id,
            LineNo = 1,
            StyleId = style.Id,
            StyleVersionId = version.Id,
            Price = 4.25m,
            LineQty = 14400m, // 12 cells × 1200
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        };
        var orderSizes = colors.SelectMany(c => sizes.Select(s => new SizeQuantity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderLineId = orderLine.Id,
            ColorCode = c,
            SizeCode = s,
            Qty = 1200m,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        })).ToArray();
        var schedules = new[]
        {
            (new DateOnly(2026, 3, 15), 8640m),
            (new DateOnly(2026, 4, 15), 5760m),
        }.Select(x => new DeliverySchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderLineId = orderLine.Id,
            ShipDate = x.Item1,
            Qty = x.Item2,
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = by,
        }).ToArray();

        // Two-phase save: the cross-module FKs (ord.order_line → pdm.*, ord.customer_order →
        // ord.buyer) are raw SQL, invisible to EF's topological insert sort — so the pdm/ord
        // parents must be committed before the order rows. Seed data, so two transactions are fine.
        db.AddRange(buyer, contact, fabric, thread, button, style, version, bom);
        db.AddRange(variants);
        db.AddRange(bomLines);
        await db.SaveChangesAsync();

        db.AddRange(order, orderLine);
        db.AddRange(orderSizes);
        db.AddRange(schedules);
        await db.SaveChangesAsync();
    }
}
