using Microsoft.EntityFrameworkCore;
using Rmg.Modules.Merchandising.Application;
using Rmg.Modules.Merchandising.Domain;

namespace Rmg.Modules.Merchandising.Infrastructure;

// Reads ride the ambient scoped DbContext (forwarded TenantScopedDbContext). Buyer/BuyerContact carry
// no global query filter, so the explicit tenant predicate on every query is the scoping — it fails
// closed too, because TenantResolutionMiddleware guarantees tid on authenticated requests.
public sealed class BuyerRepository(DbContext db) : IBuyerRepository
{
    // Keyset page over the tenant-unique code: (tenant_id, code) > cursor, ordered by code.
    public Task<Buyer[]> ListPageAsync(Guid tenantId, int take, string? afterCode, CancellationToken ct) =>
        db.Set<Buyer>().AsNoTracking()
            .Where(b => b.TenantId == tenantId && (afterCode == null || string.Compare(b.Code, afterCode) > 0))
            .OrderBy(b => b.Code)
            .Take(take)
            .ToArrayAsync(ct);

    public Task<Buyer?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<Buyer>().AsNoTracking()
            .SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, ct);

    public Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken ct) =>
        db.Set<Buyer>().AnyAsync(b => b.TenantId == tenantId && b.Code == code, ct);

    public async Task<Dictionary<Guid, BuyerContact[]>> ContactsForAsync(Guid tenantId, IReadOnlyList<Guid> buyerIds, CancellationToken ct) =>
        (await db.Set<BuyerContact>().AsNoTracking()
            .Where(c => c.TenantId == tenantId && buyerIds.Contains(c.BuyerId))
            .OrderBy(c => c.Name).ToArrayAsync(ct))
        .GroupBy(c => c.BuyerId)
        .ToDictionary(g => g.Key, g => g.ToArray());

    // Change-set only — the service adds audit + outbox rows afterwards and commits with SaveAsync.
    public Task AddAsync(Buyer buyer, IReadOnlyList<BuyerContact> contacts, CancellationToken ct)
    {
        db.Add(buyer);
        db.AddRange(contacts);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
