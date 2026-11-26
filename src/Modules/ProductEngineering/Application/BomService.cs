using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Outbox;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.ProductEngineering.Contracts;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Modules.ProductEngineering.Application;

// Persistence seam for BOMs — same change-set-only discipline as IStyleRepository: writers stage,
// the service interleaves audit + outbox, one SaveAsync commits everything atomically.
public interface IBomRepository
{
    Task<Bom[]> ListAsync(Guid tenantId, CancellationToken ct);
    Task<Bom?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Bom?> FindTrackedAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> StyleVersionExistsAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct);
    Task<bool> ExistsForVersionAsync(Guid tenantId, Guid styleVersionId, CancellationToken ct);
    Task<bool> UomExistsAsync(string code, CancellationToken ct);
    Task<bool> MaterialExistsAsync(Guid tenantId, Guid materialId, CancellationToken ct);
    Task<int> LineCountAsync(Guid tenantId, Guid bomId, CancellationToken ct);
    Task<BomLine[]> LinesForAsync(Guid tenantId, IReadOnlyList<Guid> bomIds, CancellationToken ct);
    Task AddAsync(Bom bom, CancellationToken ct);
    Task AddLineAsync(BomLine line, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<bool> RequireMakerCheckerAsync(Guid tenantId, CancellationToken ct);
}

// BOM authoring: one BOM per style version (422 BOM_EXISTS), lines only while Draft with the uom +
// material resolved against the catalogue/master (422 BOM_LINE_INVALID_UOM / MATERIAL_INVALID) and
// qty > 0 (422 QTY_POSITIVE); release needs ≥1 line and pdm.bom.approve at the API. Every command
// emits one outbox + one audit row in the same transaction.
public sealed class BomService(IBomRepository boms, IAuditWriter audit, IOutboxWriter outbox) : IBomApi
{
    public const string AuthoringChangedEvent = "pdm.bill.of.materials.authoring.changed.v1";

    public async Task<BomDto> CreateBomAsync(Guid tenantId, string createdBy, BomCreateRequest request, CancellationToken ct = default)
    {
        if (request.StyleVersionId == Guid.Empty)
            throw new ValidationProblem(new Dictionary<string, string[]> { ["styleVersionId"] = ["Style version is required."] });

        if (!await boms.StyleVersionExistsAsync(tenantId, request.StyleVersionId, ct))
            throw new NotFoundProblem(); // unknown or other tenant's version — indistinguishable by design

        if (await boms.ExistsForVersionAsync(tenantId, request.StyleVersionId, ct))
            throw new BusinessRuleProblem("BOM_EXISTS", "This style version already has a BOM (one per version).");

        var now = DateTime.UtcNow;
        var bom = new Bom
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StyleVersionId = request.StyleVersionId,
            Status = "Draft", // command semantics: creates in status Draft at revision 1
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
        };

        await boms.AddAsync(bom, ct);
        await audit.WriteAsync("pdm.bom.create", "pdm.bom", bom.Id, null,
            new { bom.StyleVersionId, bom.Status });
        await outbox.EnqueueAsync(AuthoringChangedEvent,
            new { bomId = bom.Id, styleVersionId = bom.StyleVersionId, status = bom.Status, lineCount = 0 },
            bom.Id);
        await SaveAsync(boms, "BOM", ct);

        return ToDto(bom, []);
    }

    public async Task<BomLineDto> AddBomLineAsync(Guid tenantId, Guid bomId, string actor, BomLineAddRequest request, CancellationToken ct = default)
    {
        var bom = await boms.FindTrackedAsync(tenantId, bomId, ct)
            ?? throw new NotFoundProblem(); // unknown or other tenant's id — indistinguishable by design

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.UomCode)) errors["uomCode"] = ["UOM is required."];
        if (request.MaterialId == Guid.Empty) errors["materialId"] = ["Material is required."];
        if (errors.Count > 0) throw new ValidationProblem(errors);

        // Domain rule, not shape: a non-positive consumption is a 422 with the stable code.
        if (request.QtyPerUnit <= 0)
            throw new RmgProblemException("QTY_POSITIVE", "Invalid quantity.", 422,
                new Dictionary<string, string[]> { ["qtyPerUnit"] = ["Quantity per unit must be greater than zero."] });

        var uomCode = request.UomCode.Trim();
        if (!await boms.UomExistsAsync(uomCode, ct))
            throw new RmgProblemException("BOM_LINE_INVALID_UOM", "Unknown UOM.", 422,
                new Dictionary<string, string[]> { ["uomCode"] = [uomCode] });
        if (!await boms.MaterialExistsAsync(tenantId, request.MaterialId, ct))
            throw new RmgProblemException("MATERIAL_INVALID", "Unknown material.", 422,
                new Dictionary<string, string[]> { ["materialId"] = [request.MaterialId.ToString()] });

        // Lines are authoring-time only: the Released BOM is frozen (state guard in the write).
        if (bom.Status != "Draft")
            throw new InvalidStateProblem($"BOM is {bom.Status}; lines can only be added while Draft.");

        var now = DateTime.UtcNow;
        var line = new BomLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BomId = bom.Id,
            MaterialId = request.MaterialId,
            QtyPerUnit = request.QtyPerUnit,
            UomCode = uomCode,
            WastePct = request.WastePct ?? 0m, // optional, defaults to zero waste
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = actor,
        };

        await boms.AddLineAsync(line, ct);
        await audit.WriteAsync("pdm.bom.add_line", "pdm.bom", bom.Id, null,
            new { line.MaterialId, line.QtyPerUnit, line.UomCode, line.WastePct });
        await outbox.EnqueueAsync(AuthoringChangedEvent,
            new { bomId = bom.Id, styleVersionId = bom.StyleVersionId, status = bom.Status, lineCount = await boms.LineCountAsync(tenantId, bom.Id, ct) + 1 },
            bom.Id);
        await SaveAsync(boms, "BOM", ct);

        return new BomLineDto(line.Id, line.MaterialId, line.QtyPerUnit, line.UomCode, line.WastePct);
    }

    public async Task<BomDto> ApproveBomAsync(Guid tenantId, Guid bomId, string actor, CancellationToken ct = default)
    {
        var bom = await boms.FindTrackedAsync(tenantId, bomId, ct)
            ?? throw new NotFoundProblem(); // unknown or other tenant's id — indistinguishable by design

        // Maker-checker (tenant policy flag): when on, the creator cannot release their own BOM.
        if (await boms.RequireMakerCheckerAsync(tenantId, ct) && bom.CreatedBy == actor)
            throw new BusinessRuleProblem("MAKER_CHECKER_VIOLATION", "Maker-checker is on: the creator cannot release this BOM.");

        var lineCount = await boms.LineCountAsync(tenantId, bom.Id, ct);
        var now = DateTime.UtcNow;
        bom.Release(lineCount); // InvalidStateProblem outside Draft, NO_LINES without lines
        Touch(bom, now, actor);

        await audit.WriteAsync("pdm.bom.release", "pdm.bom", bom.Id,
            new { status = "Draft" }, new { status = "Released", lines = lineCount });
        await outbox.EnqueueAsync(AuthoringChangedEvent,
            new { bomId = bom.Id, styleVersionId = bom.StyleVersionId, status = bom.Status, lineCount },
            bom.Id);
        await SaveAsync(boms, "BOM", ct);

        return ToDto(bom, await boms.LinesForAsync(tenantId, [bom.Id], ct));
    }

    public async Task<BomDto[]> ListBomsAsync(Guid tenantId, CancellationToken ct = default) =>
        await AssembleAsync(tenantId, await boms.ListAsync(tenantId, ct), ct);

    public async Task<BomDto?> FindBomAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var bom = await boms.FindAsync(tenantId, id, ct);
        if (bom is null) return null; // unknown or other tenant's id — indistinguishable by design
        return (await AssembleAsync(tenantId, [bom], ct)) is [var dto] ? dto : null;
    }

    private async Task<BomDto[]> AssembleAsync(Guid tenantId, IReadOnlyList<Bom> rows, CancellationToken ct)
    {
        var lines = await boms.LinesForAsync(tenantId, rows.Select(b => b.Id).ToArray(), ct);
        var byBom = lines.GroupBy(l => l.BomId).ToDictionary(g => g.Key, g => g.ToArray());
        return rows.Select(b => ToDto(b, byBom.GetValueOrDefault(b.Id, []))).ToArray();
    }

    private static BomDto ToDto(Bom bom, IReadOnlyList<BomLine> lines) =>
        new(bom.Id, bom.StyleVersionId, bom.Status, bom.Revision,
            lines.Select(l => new BomLineDto(l.Id, l.MaterialId, l.QtyPerUnit, l.UomCode, l.WastePct)).ToArray());

    private static void Touch(TenantEntity entity, DateTime now, string actor)
    {
        entity.Revision += 1;
        entity.UpdatedAtUtc = now;
        entity.UpdatedBy = actor;
    }

    // xmin optimistic concurrency: a lost race surfaces as DbUpdateConcurrencyException → 409
    // BOM_REVISION_CONFLICT (ConflictProblem status, aggregate-specific stable code).
    private static async Task SaveAsync(IBomRepository boms, string aggregate, CancellationToken ct)
    {
        try
        {
            await boms.SaveAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RmgProblemException($"{aggregate}_REVISION_CONFLICT", "Conflict.", 409,
                Detail: "The BOM was modified by someone else — reload and retry.");
        }
    }
}
