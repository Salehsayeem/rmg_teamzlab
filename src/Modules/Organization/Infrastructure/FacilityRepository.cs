using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Organization.Application;
using Rmg.Modules.Organization.Contracts;
using Rmg.Modules.Organization.Domain;

namespace Rmg.Modules.Organization.Infrastructure;

// Reads ride the ambient scoped DbContext — registered as the forwarded TenantScopedDbContext, whose
// facility query filter pins rows to the request tenant and fail-closes without one. The explicit
// ITenantContext predicate is defence in depth (and fails closed too: a null tenant matches no rows).
public sealed class FacilityRepository(DbContext db, ITenantContext tenant) : IFacilityRepository
{
    public async Task<FacilityDto[]> ListAsync(CancellationToken ct = default) =>
        await db.Set<Facility>().AsNoTracking()
            .Where(f => f.TenantId == tenant.TenantId)
            .OrderBy(f => f.Code)
            .Select(f => new FacilityDto(f.Id, f.Code, f.Name, f.Timezone, f.Address))
            .ToArrayAsync(ct);

    public async Task<FacilityDto?> FindAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<Facility>().AsNoTracking()
            .Where(f => f.TenantId == tenant.TenantId && f.Id == id)
            .Select(f => new FacilityDto(f.Id, f.Code, f.Name, f.Timezone, f.Address))
            .SingleOrDefaultAsync(ct);
}
