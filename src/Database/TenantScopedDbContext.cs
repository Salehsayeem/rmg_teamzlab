using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Organization.Domain;

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
    }
}
