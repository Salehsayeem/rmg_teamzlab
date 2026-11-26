using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Identity.Domain;
using Rmg.Modules.Organization.Domain;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Database;

/// <summary>Tenant-scoped view over RmgDbContext: carries the TenantEntity global query filter, which must
/// root on a context instance to re-evaluate per request — EF constantizes query filters, so a plain
/// static/ambient read is baked into the cached query plan at first execution (the documented workaround
/// for filters outside OnModelCreating, "dummy context", adapted: configurations cannot reference
/// RmgDbContext without closing a project cycle, so the context-rooted filter lives here). Non-tenant
/// reads (login, seeding) continue on the base RmgDbContext.</summary>
public class TenantScopedDbContext(DbContextOptions<RmgDbContext> options) : RmgDbContext(options)
{
    /// <summary>The ambient request tenant, mirrored from ITenantContext per request (Program.cs guard
    /// block); read by the filter below once per query execution.</summary>
    public Guid? AmbientTenantId => TenantEntity.AmbientTenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Replaces TenantEntityConfiguration's fail-closed null filter (the last HasQueryFilter call wins).
        modelBuilder.Entity<Facility>().HasQueryFilter(f => f.TenantId == AmbientTenantId);

        // Task 9 (T4 carry-over): AppUser is tenant-pinned on this context only. Its configuration
        // deliberately carries NO fail-closed filter because login (AuthUserStore), the seeder and the
        // test harness query users on the base RmgDbContext — before any tenant is known — and must
        // keep seeing them. Controller-side user reads run here, where the filter closes the leak.
        modelBuilder.Entity<AppUser>().HasQueryFilter(u => u.TenantId == AmbientTenantId);

        // Task 11 (PDM): material is tenant-pinned on this context, same as Facility above.
        modelBuilder.Entity<Material>().HasQueryFilter(m => m.TenantId == AmbientTenantId);

        // BEGIN Task 12+13 (PDM): style/version/variant/bom/bom_line tenant-pinned on this context,
        // same as Facility/AppUser/Material above — additive block only.
        modelBuilder.Entity<Style>().HasQueryFilter(s => s.TenantId == AmbientTenantId);
        modelBuilder.Entity<StyleVersion>().HasQueryFilter(v => v.TenantId == AmbientTenantId);
        modelBuilder.Entity<StyleVariant>().HasQueryFilter(v => v.TenantId == AmbientTenantId);
        modelBuilder.Entity<Bom>().HasQueryFilter(b => b.TenantId == AmbientTenantId);
        modelBuilder.Entity<BomLine>().HasQueryFilter(l => l.TenantId == AmbientTenantId);
        // END Task 12+13 (PDM)
    }
}
