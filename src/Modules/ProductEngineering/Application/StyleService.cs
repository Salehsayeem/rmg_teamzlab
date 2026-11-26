using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Modules.ProductEngineering.Contracts;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Modules.ProductEngineering.Application;

// Persistence seam for styles — implemented in module Infrastructure against the ambient scoped
// DbContext. Writers stage change sets only; the service interleaves audit + outbox between them and
// commits with one SaveAsync, so style + version + variants + audit + outbox are atomic.
public interface IStyleRepository
{
    Task<Style[]> ListAsync(Guid tenantId, CancellationToken ct);
    Task<Style?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<Style?> FindTrackedAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken ct);
    Task<StyleVersion[]> VersionsForAsync(Guid tenantId, IReadOnlyList<Guid> styleIds, CancellationToken ct);
    Task<StyleVersion?> CurrentVersionAsync(Guid tenantId, Guid styleId, CancellationToken ct);
    Task<StyleVariant[]> VariantsForAsync(Guid tenantId, IReadOnlyList<Guid> versionIds, CancellationToken ct);
    Task AddAsync(Style style, StyleVersion version, IReadOnlyList<StyleVariant> variants, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<bool> RequireMakerCheckerAsync(Guid tenantId, CancellationToken ct);
}

// Style master authoring: required fields + duplicate color/size variants are per-field 422s, taken
// codes a 422 STYLE_CODE_TAKEN — never SQL errors. Create makes Draft style + version 1 Draft +
// variant rows; approve moves style AND its current version to Approved under the ≥1 variant guard
// and the tenant's maker-checker policy. Every command emits one outbox + one audit row.
public sealed class StyleService(IStyleRepository styles, IAuditWriter audit, IOutboxWriter outbox) : IStyleApi
{
    public const string MasterChangedEvent = "pdm.style.master.creation.changed.v1";

    public async Task<StyleDto> CreateStyleAsync(Guid tenantId, string createdBy, StyleCreateRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(code)) errors["code"] = ["Code is required."];
        if (string.IsNullOrEmpty(name)) errors["name"] = ["Name is required."];
        if (request.BuyerId == Guid.Empty) errors["buyerId"] = ["Buyer is required."];

        var variants = (request.Variants ?? []).Select(v => new StyleVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ColorCode = v.ColorCode?.Trim() ?? "",
            SizeCode = v.SizeCode?.Trim() ?? "",
        }).ToArray();

        if (variants.Any(v => v.ColorCode.Length == 0 || v.SizeCode.Length == 0))
            errors["variants"] = ["Every variant needs a color code and a size code."];

        if (errors.Count > 0) throw new ValidationProblem(errors);

        // Duplicate matrix cells are a domain rule, not a shape problem: 422 with the stable code
        // (the unique index (tenant_id, style_version_id, color_code, size_code) is the backstop).
        var duplicates = variants.GroupBy(v => (v.ColorCode, v.SizeCode)).Where(g => g.Count() > 1)
            .Select(g => $"({g.Key.ColorCode}, {g.Key.SizeCode})").ToArray();
        if (duplicates.Length > 0)
            throw new RmgProblemException("STYLE_VARIANT_DUPLICATE", "Duplicate variants.", 422,
                new Dictionary<string, string[]> { ["variants"] = [$"Duplicate color/size combination(s): {string.Join(", ", duplicates)}"] });

        if (await styles.CodeExistsAsync(tenantId, code!, ct))
            throw new BusinessRuleProblem("STYLE_CODE_TAKEN", $"Style code '{code}' is already in use.");

        var now = DateTime.UtcNow;
        var style = new Style
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code!,
            Name = name!,
            Season = request.Season?.Trim(),
            Category = request.Category?.Trim(),
            BuyerId = request.BuyerId,
            Status = "Draft", // command semantics: creates in status Draft at revision 1
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
        };
        var version = new StyleVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StyleId = style.Id,
            Version = 1,
            Status = "Draft",
            Revision = 1,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
        };

        // No navigations between the entities — the FK columns must be set by hand (baseline NOT NULLs too).
        foreach (var v in variants) { v.StyleVersionId = version.Id; v.CreatedAtUtc = now; v.CreatedBy = createdBy; }

        await styles.AddAsync(style, version, variants, ct);
        await audit.WriteAsync("pdm.style.create", "pdm.style", style.Id, null,
            new { style.Code, style.Name, style.Season, style.Category, buyerId = style.BuyerId, variants = variants.Length });
        await outbox.EnqueueAsync(MasterChangedEvent,
            new
            {
                styleId = style.Id,
                code = style.Code,
                name = style.Name,
                buyerId = style.BuyerId,
                status = style.Status,
                version = version.Version,
                variantCount = variants.Length,
            },
            style.Id);
        await styles.SaveAsync(ct);

        return ToDto(style, [ToVersionDto(version, variants)]);
    }

    public async Task<StyleDto> ApproveStyleAsync(Guid tenantId, Guid styleId, string actor, CancellationToken ct = default)
    {
        var style = await styles.FindTrackedAsync(tenantId, styleId, ct)
            ?? throw new NotFoundProblem(); // unknown or other tenant's id — indistinguishable by design

        // Maker-checker (tenant policy flag): when on, the creator cannot approve their own style.
        if (await styles.RequireMakerCheckerAsync(tenantId, ct) && style.CreatedBy == actor)
            throw new BusinessRuleProblem("MAKER_CHECKER_VIOLATION", "Maker-checker is on: the creator cannot approve this style.");

        // "Current version" = highest version (pilot only ever creates version 1).
        var version = await styles.CurrentVersionAsync(tenantId, styleId, ct)
            ?? throw new BusinessRuleProblem("NO_VERSION", "The style has no version to approve.");
        var variants = await styles.VariantsForAsync(tenantId, [version.Id], ct);

        var now = DateTime.UtcNow;
        style.Approve(variants.Length); // InvalidStateProblem outside Draft, NO_VARIANTS without variants
        version.Approve(now);           // InvalidStateProblem outside Draft
        Touch(style, now, actor);
        Touch(version, now, actor);

        await audit.WriteAsync("pdm.style.approve", "pdm.style", style.Id,
            new { status = "Draft" }, new { status = "Approved", approvedVersion = version.Version });
        await outbox.EnqueueAsync(MasterChangedEvent,
            new { styleId = style.Id, code = style.Code, status = style.Status, approvedVersion = version.Version },
            style.Id);
        await SaveAsync(styles, "STYLE", ct);

        return ToDto(style, [ToVersionDto(version, variants)]);
    }

    public async Task<StyleDto[]> ListStylesAsync(Guid tenantId, CancellationToken ct = default) =>
        await AssembleAsync(tenantId, await styles.ListAsync(tenantId, ct), ct);

    public async Task<StyleDto?> FindStyleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var style = await styles.FindAsync(tenantId, id, ct);
        if (style is null) return null; // unknown or other tenant's id — indistinguishable by design
        return (await AssembleAsync(tenantId, [style], ct)) is [var dto] ? dto : null;
    }

    private async Task<StyleDto[]> AssembleAsync(Guid tenantId, IReadOnlyList<Style> rows, CancellationToken ct)
    {
        var versions = await styles.VersionsForAsync(tenantId, rows.Select(s => s.Id).ToArray(), ct);
        var variants = await styles.VariantsForAsync(tenantId, versions.Select(v => v.Id).ToArray(), ct);
        var byVersion = variants.GroupBy(v => v.StyleVersionId).ToDictionary(g => g.Key, g => g.ToArray());
        return rows.Select(s => ToDto(s,
            versions.Where(v => v.StyleId == s.Id).OrderByDescending(v => v.Version)
                .Select(v => ToVersionDto(v, byVersion.GetValueOrDefault(v.Id, []))).ToArray())).ToArray();
    }

    private static StyleDto ToDto(Style style, StyleVersionDto[] versions) =>
        new(style.Id, style.Code, style.Name, style.Season, style.Category, style.BuyerId, style.Status, style.Revision, versions);

    private static StyleVersionDto ToVersionDto(StyleVersion version, IReadOnlyList<StyleVariant> variants) =>
        new(version.Id, version.Version, version.Status, version.ApprovedAtUtc,
            variants.Select(v => new StyleVariantDto(v.Id, v.ColorCode, v.SizeCode)).ToArray());

    // Both written aggregates carry the TenantEntity baseline — bump on every successful write.
    private static void Touch(Rmg.BuildingBlocks.Tenancy.TenantEntity entity, DateTime now, string actor)
    {
        entity.Revision += 1;
        entity.UpdatedAtUtc = now;
        entity.UpdatedBy = actor;
    }

    // xmin optimistic concurrency: a lost race surfaces as DbUpdateConcurrencyException → 409
    // <AGG>_REVISION_CONFLICT (ConflictProblem status, aggregate-specific stable code).
    private static async Task SaveAsync(IStyleRepository styles, string aggregate, CancellationToken ct)
    {
        try
        {
            await styles.SaveAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RmgProblemException($"{aggregate}_REVISION_CONFLICT", "Conflict.", 409,
                Detail: "The style was modified by someone else — reload and retry.");
        }
    }
}
