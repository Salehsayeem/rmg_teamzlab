using Microsoft.EntityFrameworkCore;
using Rmg.Modules.ProductEngineering.Application;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Modules.ProductEngineering.Infrastructure;

// Same scoping + change-set-only discipline as StyleRepository (see there), for BOM headers/lines.
public sealed class BomRepository(DbContext db) : IBomRepository
{
    public Task<Bom[]> ListAsync(Guid tenantId, CancellationToken ct) =>
        db.Set<Bom>().AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .OrderBy(b => b.CreatedAtUtc)
            .ToArrayAsync(ct);

    public Task<Bom?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<Bom>().AsNoTracking()
            .SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, ct);

    public Task<Bom?> FindTrackedAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<Bom>()
            .SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, ct);

    public Task<bool> StyleVersionExistsAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct) =>
        db.Set<StyleVersion>().AnyAsync(v => v.TenantId == tenantId && v.Id == styleVersionId, ct);

    public Task<bool> ExistsForVersionAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct) =>
        db.Set<Bom>().AnyAsync(b => b.TenantId == tenantId && b.StyleVersionId == styleVersionId, ct);

    public Task<bool> UomExistsAsync(string code, CancellationToken ct) =>
        db.Set<Uom>().AnyAsync(u => u.Code == code, ct);

    public Task<bool> MaterialExistsAsync(Guid tenantId, Guid materialId, CancellationToken ct) =>
        db.Set<Material>().AnyAsync(m => m.TenantId == tenantId && m.Id == materialId, ct);

    public Task<int> LineCountAsync(Guid tenantId, Guid bomId, CancellationToken ct) =>
        db.Set<BomLine>().CountAsync(l => l.TenantId == tenantId && l.BomId == bomId, ct);

    public Task<BomLine[]> LinesForAsync(Guid tenantId, IReadOnlyList<Guid> bomIds, CancellationToken ct) =>
        db.Set<BomLine>().AsNoTracking()
            .Where(l => l.TenantId == tenantId && bomIds.Contains(l.BomId))
            .OrderBy(l => l.CreatedAtUtc)
            .ToArrayAsync(ct);

    // Change-set only — the service adds audit + outbox rows afterwards and commits with SaveAsync.
    public Task AddAsync(Bom bom, CancellationToken ct)
    {
        db.Add(bom);
        return Task.CompletedTask;
    }

    public Task AddLineAsync(BomLine line, CancellationToken ct)
    {
        db.Add(line);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // org.tenant is another module's table — raw parameterized read (see StyleRepository).
    public Task<bool> RequireMakerCheckerAsync(Guid tenantId, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"SELECT require_maker_checker AS \"Value\" FROM org.tenant WHERE id = {tenantId}")
            .SingleAsync(ct);
}
