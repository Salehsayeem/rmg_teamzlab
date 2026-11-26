using Microsoft.EntityFrameworkCore;
using Rmg.Modules.Merchandising.Application;
using Rmg.Modules.Merchandising.Domain;

namespace Rmg.Modules.Merchandising.Infrastructure;

// Reads ride the ambient scoped DbContext (forwarded TenantScopedDbContext, whose query filter pins
// ord rows to the request tenant) plus an explicit tenant predicate on every query — defence in
// depth that fails closed either way. Writers stage change sets only (BuyerRepository discipline).
public sealed class OrderRepository(DbContext db) : IOrderRepository
{
    // Keyset page over the tenant-unique code: (tenant_id, code) > cursor, ordered by code.
    public Task<CustomerOrder[]> ListPageAsync(Guid tenantId, int take, string? afterCode, CancellationToken ct) =>
        db.Set<CustomerOrder>().AsNoTracking()
            .Where(o => o.TenantId == tenantId && (afterCode == null || string.Compare(o.Code, afterCode) > 0))
            .OrderBy(o => o.Code)
            .Take(take)
            .ToArrayAsync(ct);

    public Task<CustomerOrder?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<CustomerOrder>().AsNoTracking()
            .SingleOrDefaultAsync(o => o.TenantId == tenantId && o.Id == id, ct);

    // Tracked variant for the confirm/cancel writes (state guards mutate the row).
    public Task<CustomerOrder?> FindTrackedAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<CustomerOrder>()
            .SingleOrDefaultAsync(o => o.TenantId == tenantId && o.Id == id, ct);

    public Task<bool> BuyerPoExistsAsync(Guid tenantId, Guid buyerId, string buyerPo, CancellationToken ct) =>
        db.Set<CustomerOrder>().AnyAsync(o => o.TenantId == tenantId && o.BuyerId == buyerId && o.BuyerPo == buyerPo, ct);

    // Ruling W2-b: the capture validations cross into pdm (version status + variant matrix) — those
    // rows belong to another module, so they are parameterized raw reads (no EF navigation, no
    // cross-module reference). The composite predicate pins the tenant; no filter to bypass.
    public Task<StyleVersionRef?> StyleVersionAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct) =>
        db.Database.SqlQuery<StyleVersionRef>($"""
            SELECT style_id AS "StyleId", status AS "Status"
            FROM pdm.style_version
            WHERE tenant_id = {tenantId} AND id = {styleVersionId}
            """).SingleOrDefaultAsync(ct);

    public Task<VariantCell[]> VariantsForAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct) =>
        db.Database.SqlQuery<VariantCell>($"""
            SELECT color_code AS "ColorCode", size_code AS "SizeCode"
            FROM pdm.style_variant
            WHERE tenant_id = {tenantId} AND style_version_id = {styleVersionId}
            """).ToArrayAsync(ct);

    public Task<OrderLine[]> LinesForAsync(Guid tenantId, IReadOnlyList<Guid> orderIds, CancellationToken ct) =>
        db.Set<OrderLine>().AsNoTracking()
            .Where(l => l.TenantId == tenantId && orderIds.Contains(l.OrderId))
            .ToArrayAsync(ct);

    public async Task<(SizeQuantity[] Sizes, DeliverySchedule[] Schedules)> LineChildrenAsync(Guid tenantId, IReadOnlyList<Guid> lineIds, CancellationToken ct)
    {
        var sizes = await db.Set<SizeQuantity>().AsNoTracking()
            .Where(s => s.TenantId == tenantId && lineIds.Contains(s.OrderLineId))
            .ToArrayAsync(ct);
        var schedules = await db.Set<DeliverySchedule>().AsNoTracking()
            .Where(d => d.TenantId == tenantId && lineIds.Contains(d.OrderLineId))
            .ToArrayAsync(ct);
        return (sizes, schedules);
    }

    // Change-set only — the service adds audit + outbox rows afterwards and commits with SaveAsync.
    public Task AddAsync(CustomerOrder order, OrderLine[] lines, SizeQuantity[] sizes, DeliverySchedule[] schedules, CancellationToken ct)
    {
        db.Add(order);
        db.AddRange(lines);
        db.AddRange(sizes);
        db.AddRange(schedules);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // Maker-checker policy flag: org.tenant belongs to another module (modules reference Common+
    // Tenancy only, no EF navigation across modules) — a parameterized raw read of the one column.
    public Task<bool> RequireMakerCheckerAsync(Guid tenantId, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"SELECT require_maker_checker AS \"Value\" FROM org.tenant WHERE id = {tenantId}")
            .SingleAsync(ct);
}
