using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.ProductEngineering.Application;
using Rmg.Modules.ProductEngineering.Contracts;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Modules.ProductEngineering.Infrastructure;

// Reads ride the ambient scoped DbContext — registered as the forwarded TenantScopedDbContext, whose
// query filter pins materials to the request tenant and fail-closes without one. UOMs are global
// reference data (no filter). The explicit ITenantContext predicate is defence in depth (and fails
// closed too: a null tenant matches no rows).
public sealed class MaterialRepository(DbContext db, ITenantContext tenant) : IMaterialRepository
{
    public Task<UomDto[]> ListUomsAsync(CancellationToken ct = default) =>
        db.Set<Uom>().AsNoTracking()
            .OrderBy(u => u.Code)
            .Select(u => new UomDto(u.Code, u.Name))
            .ToArrayAsync(ct);

    public Task<MaterialDto[]> ListAsync(CancellationToken ct = default) =>
        db.Set<Material>().AsNoTracking()
            .Where(m => m.TenantId == tenant.TenantId)
            .OrderBy(m => m.Code)
            .Select(m => new MaterialDto(m.Id, m.Code, m.Name, m.Category, m.BaseUomCode))
            .ToArrayAsync(ct);

    public Task<bool> UomExistsAsync(string code, CancellationToken ct = default) =>
        db.Set<Uom>().AnyAsync(u => u.Code == code, ct);

    public Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken ct = default) =>
        db.Set<Material>().AnyAsync(m => m.TenantId == tenantId && m.Code == code, ct);

    public async Task AddAsync(Material material, CancellationToken ct = default)
    {
        db.Add(material);
        await db.SaveChangesAsync(ct);
    }
}
