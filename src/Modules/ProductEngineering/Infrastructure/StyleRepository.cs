using Microsoft.EntityFrameworkCore;
using Rmg.Modules.ProductEngineering.Application;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Modules.ProductEngineering.Infrastructure;

// Reads ride the ambient scoped DbContext (forwarded TenantScopedDbContext, whose query filter pins
// pdm rows to the request tenant) plus an explicit tenant predicate on every query — defence in
// depth that fails closed either way. Writers stage change sets only (BuyerRepository discipline).
public sealed class StyleRepository(DbContext db) : IStyleRepository
{
    public Task<Style[]> ListAsync(Guid tenantId, CancellationToken ct) =>
        db.Set<Style>().AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Code)
            .ToArrayAsync(ct);

    public Task<Style?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<Style>().AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

    // Tracked variant for the approve write (state guards mutate the row).
    public Task<Style?> FindTrackedAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<Style>()
            .SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

    public Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken ct) =>
        db.Set<Style>().AnyAsync(s => s.TenantId == tenantId && s.Code == code, ct);

    public Task<StyleVersion[]> VersionsForAsync(Guid tenantId, IReadOnlyList<Guid> styleIds, CancellationToken ct) =>
        db.Set<StyleVersion>().AsNoTracking()
            .Where(v => v.TenantId == tenantId && styleIds.Contains(v.StyleId))
            .ToArrayAsync(ct);

    public Task<StyleVersion?> CurrentVersionAsync(Guid tenantId, Guid styleId, CancellationToken ct) =>
        db.Set<StyleVersion>()
            .Where(v => v.TenantId == tenantId && v.StyleId == styleId)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(ct);

    public Task<StyleVariant[]> VariantsForAsync(Guid tenantId, IReadOnlyList<Guid> versionIds, CancellationToken ct) =>
        db.Set<StyleVariant>().AsNoTracking()
            .Where(v => v.TenantId == tenantId && versionIds.Contains(v.StyleVersionId))
            .OrderBy(v => v.ColorCode).ThenBy(v => v.SizeCode)
            .ToArrayAsync(ct);

    // Change-set only — the service adds audit + outbox rows afterwards and commits with SaveAsync.
    public Task AddAsync(Style style, StyleVersion version, IReadOnlyList<StyleVariant> variants, CancellationToken ct)
    {
        db.Add(style);
        db.Add(version);
        db.AddRange(variants);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // Maker-checker policy flag: org.tenant belongs to another module (modules reference Common+
    // Tenancy only, no EF navigation across modules) — a parameterized raw read of the one column.
    public Task<bool> RequireMakerCheckerAsync(Guid tenantId, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"SELECT require_maker_checker AS \"Value\" FROM org.tenant WHERE id = {tenantId}")
            .SingleAsync(ct);
}
