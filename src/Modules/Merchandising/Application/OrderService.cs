using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Modules.Merchandising.Contracts;
using Rmg.Modules.Merchandising.Domain;

namespace Rmg.Modules.Merchandising.Application;

// Persistence seam for customer orders — the BuyerRepository discipline: writers stage change sets
// only; the service interleaves audit + outbox between them and commits with one SaveAsync, so the
// order + lines + sizes + schedules + audit + outbox rows are atomic. The pdm reads (style version
// status + variant matrix) are cross-module and answered by parameterized raw SQL in the repository
// (ruling W2-b — no EF navigation and no cross-module reference).
public interface IOrderRepository
{
    Task<CustomerOrder[]> ListPageAsync(Guid tenantId, int take, string? afterCode, CancellationToken ct);
    Task<CustomerOrder?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<CustomerOrder?> FindTrackedAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> BuyerPoExistsAsync(Guid tenantId, Guid buyerId, string buyerPo, CancellationToken ct);
    Task<StyleVersionRef?> StyleVersionAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct);
    Task<VariantCell[]> VariantsForAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct);
    Task<OrderLine[]> LinesForAsync(Guid tenantId, IReadOnlyList<Guid> orderIds, CancellationToken ct);
    Task<(SizeQuantity[] Sizes, DeliverySchedule[] Schedules)> LineChildrenAsync(Guid tenantId, IReadOnlyList<Guid> lineIds, CancellationToken ct);
    Task AddAsync(CustomerOrder order, OrderLine[] lines, SizeQuantity[] sizes, DeliverySchedule[] schedules, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<bool> RequireMakerCheckerAsync(Guid tenantId, CancellationToken ct);
}

// Cross-module raw read models (pdm.style_version / pdm.style_variant); property names map the
// SQL column aliases — SqlQuery<T> binds them positionally.
public sealed class StyleVersionRef
{
    public Guid StyleId { get; set; }
    public string Status { get; set; } = null!;
}

public sealed class VariantCell
{
    public string ColorCode { get; set; } = null!;
    public string SizeCode { get; set; } = null!;
}

// Customer order capture + lifecycle. Capture validates shape (400), quantities (422 QTY_POSITIVE
// per-field), duplicate buyer PO (422), the style version Approved gate (422
// STYLE_VERSION_NOT_APPROVED) and every ordered (color,size) against that version's variant matrix
// (422 ORDER_VARIANT_MISMATCH per-field), then stores lines with derived line_qty = Σ sizes.
// Confirm is revision-guarded (409 ORDER_REVISION_CONFLICT, expected + xmin) and maker-checker
// guarded; cancel moves Draft/Confirmed→Cancelled. Every command emits the same outbox event plus
// its own audit row in the same transaction; buyer/style existence is enforced by the raw composite
// FKs alone (pilot ruling — no service-level existence checks for cross-module rows).
public sealed class OrderService(IOrderRepository orders, IAuditWriter audit, IOutboxWriter outbox) : IOrderApi
{
    public const string CaptureChangedEvent = "ord.customer.order.capture.changed.v1";

    public async Task<OrderDto> CaptureOrderAsync(Guid tenantId, string actor, Guid? facilityId, OrderCreateRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var code = request.Code?.Trim();
        var buyerPo = request.BuyerPo?.Trim();
        var currency = request.Currency?.Trim();
        if (string.IsNullOrEmpty(code)) errors["code"] = ["Code is required."];
        if (request.BuyerId == Guid.Empty) errors["buyerId"] = ["Buyer is required."];
        if (string.IsNullOrEmpty(buyerPo)) errors["buyerPo"] = ["Buyer PO is required."];
        if (string.IsNullOrEmpty(currency)) errors["currency"] = ["Currency is required."];
        else if (currency.Length != 3) errors["currency"] = ["Currency must be a 3-letter ISO 4217 code."];
        if (request.OrderDate == default) errors["orderDate"] = ["Order date is required."];

        var lineInputs = request.Lines ?? [];
        if (lineInputs.Length == 0) errors["lines"] = ["An order needs at least one line."];
        else
        {
            for (var i = 0; i < lineInputs.Length; i++)
            {
                var line = lineInputs[i];
                if (line.StyleVersionId == Guid.Empty)
                    errors[$"lines[{i}].styleVersionId"] = ["Style version is required."];
                if (line.Price < 0)
                    errors[$"lines[{i}].price"] = ["Price must be zero or greater."];
                if ((line.Sizes ?? []).Length == 0)
                    errors[$"lines[{i}].sizes"] = ["A line needs at least one size quantity."];
                // A missing color/size on a cell is a shape problem, not a variant mismatch: the
                // domain loop below dereferences these, so nulls must never reach it.
                for (var j = 0; j < (line.Sizes ?? []).Length; j++)
                    if (string.IsNullOrWhiteSpace(line.Sizes![j].ColorCode) || string.IsNullOrWhiteSpace(line.Sizes![j].SizeCode))
                        errors[$"lines[{i}].sizes[{j}]"] = ["Every size quantity needs a color code and a size code."];
                for (var j = 0; j < (line.Schedules ?? []).Length; j++)
                    if (line.Schedules![j].ShipDate == default)
                        errors[$"lines[{i}].schedules[{j}].shipDate"] = ["Ship date is required."];
            }
        }
        if (errors.Count > 0) throw new ValidationProblem(errors);

        // A non-positive quantity is a domain rule (DB CHECK qty > 0 is the backstop), not a shape
        // problem: 422 QTY_POSITIVE per-field, exactly like BOM lines.
        var qtyErrors = new Dictionary<string, string[]>();
        for (var i = 0; i < lineInputs.Length; i++)
        {
            for (var j = 0; j < (lineInputs[i].Sizes ?? []).Length; j++)
                if (lineInputs[i].Sizes![j].Qty <= 0)
                    qtyErrors[$"lines[{i}].sizes[{j}].qty"] = ["Quantity must be greater than zero."];
            for (var j = 0; j < (lineInputs[i].Schedules ?? []).Length; j++)
                if (lineInputs[i].Schedules![j].Qty <= 0)
                    qtyErrors[$"lines[{i}].schedules[{j}].qty"] = ["Quantity must be greater than zero."];
        }
        if (qtyErrors.Count > 0)
            throw new RmgProblemException("QTY_POSITIVE", "Invalid quantity.", 422, qtyErrors);

        if (await orders.BuyerPoExistsAsync(tenantId, request.BuyerId, buyerPo!, ct))
            throw new BusinessRuleProblem("DUPLICATE_BUYER_PO", $"Buyer PO '{buyerPo}' already exists for this buyer.");

        var now = DateTime.UtcNow;
        var order = new CustomerOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FacilityId = facilityId,
            Code = code!,
            BuyerId = request.BuyerId,
            BuyerPo = buyerPo!,
            Currency = currency!,
            OrderDate = request.OrderDate,
            Status = "Draft", // command semantics: capture creates in status Draft at revision 1
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = actor,
        };

        var lines = new OrderLine[lineInputs.Length];
        var sizeRows = new List<SizeQuantity>();
        var scheduleRows = new List<DeliverySchedule>();
        for (var i = 0; i < lineInputs.Length; i++)
        {
            var input = lineInputs[i];

            // Capture pins an immutable revision: the version must exist (404 — unknown or another
            // tenant's id, indistinguishable by design) and be Approved (422 stable code).
            var version = await orders.StyleVersionAsync(tenantId, input.StyleVersionId, ct)
                ?? throw new NotFoundProblem("Unknown style version.");
            if (version.Status != "Approved")
                throw new BusinessRuleProblem("STYLE_VERSION_NOT_APPROVED",
                    $"Style version is {version.Status}; orders pin Approved revisions only.");

            // Every ordered (color,size) must exist in that version's variant matrix (per-field 422);
            // a duplicated cell within the line is flagged the same way (the unique index is the backstop).
            var variants = await orders.VariantsForAsync(tenantId, input.StyleVersionId, ct);
            var matrix = variants.Select(v => (v.ColorCode, v.SizeCode)).ToHashSet();
            var seen = new HashSet<(string, string)>();
            var mismatch = new Dictionary<string, string[]>();
            for (var j = 0; j < (input.Sizes ?? []).Length; j++)
            {
                var cell = input.Sizes![j];
                var color = cell.ColorCode!.Trim();
                var size = cell.SizeCode!.Trim();
                if (!matrix.Contains((color, size)))
                    mismatch[$"lines[{i}].sizes[{j}]"] = [$"({color}, {size}) is not in the style version's variant matrix."];
                else if (!seen.Add((color, size)))
                    mismatch[$"lines[{i}].sizes[{j}]"] = [$"({color}, {size}) is duplicated on this line."];
            }
            if (mismatch.Count > 0)
                throw new RmgProblemException("ORDER_VARIANT_MISMATCH", "Variant mismatch.", 422, mismatch);

            var line = new OrderLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = order.Id,
                LineNo = i + 1,
                StyleId = version.StyleId, // derived from the pinned version — no separate client input
                StyleVersionId = input.StyleVersionId,
                Price = input.Price,
                LineQty = input.Sizes!.Sum(s => s.Qty), // derived = Σ sizes, never client-supplied
                Revision = 1,
                CreatedAtUtc = now,
                CreatedBy = actor,
            };
            lines[i] = line;
            sizeRows.AddRange(input.Sizes!.Select(s => new SizeQuantity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderLineId = line.Id,
                ColorCode = s.ColorCode!.Trim(),
                SizeCode = s.SizeCode!.Trim(),
                Qty = s.Qty,
                Revision = 1,
                CreatedAtUtc = now,
                CreatedBy = actor,
            }));
            scheduleRows.AddRange((input.Schedules ?? []).Select(s => new DeliverySchedule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderLineId = line.Id,
                ShipDate = s.ShipDate,
                Qty = s.Qty,
                Revision = 1,
                CreatedAtUtc = now,
                CreatedBy = actor,
            }));
        }

        // No navigations between the entities — the FK columns were set by hand (baseline NOT NULLs too).
        await orders.AddAsync(order, lines, [.. sizeRows], [.. scheduleRows], ct);
        await audit.WriteAsync("ord.order.create", "ord.customer_order", order.Id, null,
            new { order.Code, order.BuyerId, order.BuyerPo, order.Currency, order.OrderDate, order.Status, lines = lines.Length });
        await outbox.EnqueueAsync(CaptureChangedEvent,
            new { orderId = order.Id, code = order.Code, buyerPo = order.BuyerPo, status = order.Status, lineCount = lines.Length },
            order.Id);
        await SaveAsync(ct);

        return await AssembleAsync(tenantId, [order], ct) is [var dto] ? dto : throw new InvalidOperationException("Captured order vanished.");
    }

    public async Task<OrderDto> ConfirmOrderAsync(Guid tenantId, Guid orderId, string actor, OrderConfirmRequest request, CancellationToken ct = default)
    {
        var order = await orders.FindTrackedAsync(tenantId, orderId, ct)
            ?? throw new NotFoundProblem(); // unknown or other tenant's id — indistinguishable by design

        // Maker-checker (tenant policy flag): when on, the creator cannot confirm their own order.
        if (await orders.RequireMakerCheckerAsync(tenantId, ct) && order.CreatedBy == actor)
            throw new BusinessRuleProblem("MAKER_CHECKER_VIOLATION", "Maker-checker is on: the creator cannot confirm this order.");

        // The caller's expected revision must match: a stale caller stands in a concurrent writer.
        if (order.Revision != request.ExpectedRevision)
            throw new RevisionConflictProblem("ORDER_REVISION_CONFLICT",
                $"expected revision {request.ExpectedRevision}, current {order.Revision}.");

        var statusBefore = order.Status;
        var now = DateTime.UtcNow;
        order.Confirm(); // InvalidStateProblem outside Draft
        Touch(order, now, actor);

        await audit.WriteAsync("ord.order.confirm", "ord.customer_order", order.Id,
            new { status = statusBefore }, new { status = order.Status });
        await outbox.EnqueueAsync(CaptureChangedEvent,
            new { orderId = order.Id, code = order.Code, buyerPo = order.BuyerPo, status = order.Status },
            order.Id);
        await SaveAsync(ct);

        return await AssembleAsync(tenantId, [order], ct) is [var dto] ? dto : throw new InvalidOperationException("Confirmed order vanished.");
    }

    public async Task<OrderDto> CancelOrderAsync(Guid tenantId, Guid orderId, string actor, OrderCancelRequest request, CancellationToken ct = default)
    {
        var order = await orders.FindTrackedAsync(tenantId, orderId, ct)
            ?? throw new NotFoundProblem(); // unknown or other tenant's id — indistinguishable by design

        var statusBefore = order.Status;
        var now = DateTime.UtcNow;
        order.Cancel(); // InvalidStateProblem when already Cancelled
        Touch(order, now, actor);

        await audit.WriteAsync("ord.order.cancel", "ord.customer_order", order.Id,
            new { status = statusBefore }, new { status = order.Status }, reason: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());
        await outbox.EnqueueAsync(CaptureChangedEvent,
            new { orderId = order.Id, code = order.Code, buyerPo = order.BuyerPo, status = order.Status },
            order.Id);
        await SaveAsync(ct);

        return await AssembleAsync(tenantId, [order], ct) is [var dto] ? dto : throw new InvalidOperationException("Cancelled order vanished.");
    }

    public async Task<(OrderDto[] Orders, PageMeta Meta)> ListOrdersAsync(Guid tenantId, int pageSize, string? cursor, CancellationToken ct = default)
    {
        // Keyset pagination on the tenant-scoped code: the cursor is the last page's last code.
        var (size, afterCode) = Page.Normalize(pageSize, cursor);
        var rows = await orders.ListPageAsync(tenantId, size + 1, afterCode, ct);
        var page = rows.Take(size).ToArray();
        string? nextCursor = rows.Length > size ? page[^1].Code : null;

        var dtos = await AssembleAsync(tenantId, page, ct);
        return (dtos, new PageMeta(size, nextCursor, null));
    }

    public async Task<OrderDto?> FindOrderAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var order = await orders.FindAsync(tenantId, id, ct);
        if (order is null) return null; // unknown or other tenant's id — indistinguishable by design
        return (await AssembleAsync(tenantId, [order], ct)) is [var dto] ? dto : null;
    }

    private async Task<OrderDto[]> AssembleAsync(Guid tenantId, IReadOnlyList<CustomerOrder> rows, CancellationToken ct)
    {
        var lines = await orders.LinesForAsync(tenantId, rows.Select(o => o.Id).ToArray(), ct);
        var (sizes, schedules) = await orders.LineChildrenAsync(tenantId, lines.Select(l => l.Id).ToArray(), ct);
        var sizesByLine = sizes.GroupBy(s => s.OrderLineId).ToDictionary(g => g.Key, g => g.ToArray());
        var schedulesByLine = schedules.GroupBy(s => s.OrderLineId).ToDictionary(g => g.Key, g => g.ToArray());
        var linesByOrder = lines.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.OrderBy(l => l.LineNo).ToArray());
        return rows.Select(o => ToDto(o, linesByOrder.GetValueOrDefault(o.Id, []), sizesByLine, schedulesByLine)).ToArray();
    }

    private static OrderDto ToDto(CustomerOrder order, IReadOnlyList<OrderLine> lines,
        Dictionary<Guid, SizeQuantity[]> sizesByLine, Dictionary<Guid, DeliverySchedule[]> schedulesByLine) =>
        new(order.Id, order.Code, order.BuyerId, order.BuyerPo, order.Currency, order.OrderDate, order.Status, order.Revision,
            lines.Select(l => new OrderLineDto(l.Id, l.LineNo, l.StyleId, l.StyleVersionId, l.Price, l.LineQty,
                sizesByLine.GetValueOrDefault(l.Id, []).Select(s => new OrderSizeDto(s.ColorCode, s.SizeCode, s.Qty)).ToArray(),
                schedulesByLine.GetValueOrDefault(l.Id, []).Select(s => new OrderScheduleDto(s.ShipDate, s.Qty)).ToArray())).ToArray());

    // The written aggregate carries the TenantEntity baseline — bump on every successful write.
    private static void Touch(Rmg.BuildingBlocks.Tenancy.TenantEntity entity, DateTime now, string actor)
    {
        entity.Revision += 1;
        entity.UpdatedAtUtc = now;
        entity.UpdatedBy = actor;
    }

    // xmin optimistic concurrency: a lost race surfaces as DbUpdateConcurrencyException → 409
    // ORDER_REVISION_CONFLICT (ConflictProblem status, aggregate-specific stable code).
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await orders.SaveAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RmgProblemException("ORDER_REVISION_CONFLICT", "Conflict.", 409,
                Detail: "The order was modified by someone else — reload and retry.");
        }
    }
}
