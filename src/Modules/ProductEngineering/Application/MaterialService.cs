using Rmg.BuildingBlocks.Common;
using Rmg.Modules.ProductEngineering.Contracts;
using Rmg.Modules.ProductEngineering.Domain;

namespace Rmg.Modules.ProductEngineering.Application;

// Persistence seam for materials and UOMs — implemented in module Infrastructure against the scoped
// tenant context. Lists are tenant-scoped in the repository (ambient ITenantContext, fail closed);
// create takes explicit tenantId/createdBy from the controller, mirroring UserService.
public interface IMaterialRepository
{
    Task<UomDto[]> ListUomsAsync(CancellationToken ct = default);
    Task<MaterialDto[]> ListAsync(CancellationToken ct = default);
    Task<bool> UomExistsAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid tenantId, string code, CancellationToken ct = default);
    Task AddAsync(Material material, CancellationToken ct = default);
}

// Material master: required fields, base UOM resolved against the global catalogue (422 UOM_INVALID
// with a per-field error), codes unique per tenant (422 DUPLICATE_CODE) — bad requests are 422s,
// never SQL errors.
public sealed class MaterialService(IMaterialRepository materials) : IPdmApi
{
    public Task<UomDto[]> ListUomsAsync(CancellationToken ct = default) => materials.ListUomsAsync(ct);

    public Task<MaterialDto[]> ListMaterialsAsync(CancellationToken ct = default) => materials.ListAsync(ct);

    public async Task<MaterialDto> CreateMaterialAsync(Guid tenantId, string createdBy, MaterialCreateRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Code)) errors["code"] = ["Code is required."];
        if (string.IsNullOrWhiteSpace(request.Name)) errors["name"] = ["Name is required."];
        if (string.IsNullOrWhiteSpace(request.BaseUomCode)) errors["baseUomCode"] = ["Base UOM is required."];
        if (errors.Count > 0) throw new ValidationProblem(errors);

        var baseUomCode = request.BaseUomCode.Trim();
        if (!await materials.UomExistsAsync(baseUomCode, ct))
            throw new RmgProblemException("UOM_INVALID", "Unknown base UOM.", 422,
                new Dictionary<string, string[]> { ["baseUomCode"] = [baseUomCode] });

        var code = request.Code.Trim();
        if (await materials.CodeExistsAsync(tenantId, code, ct))
            throw new BusinessRuleProblem("DUPLICATE_CODE", $"Material code '{code}' is already in use.");

        var material = new Material
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            Category = request.Category?.Trim(),
            BaseUomCode = baseUomCode,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
        await materials.AddAsync(material, ct);
        return new MaterialDto(material.Id, material.Code, material.Name, material.Category, material.BaseUomCode);
    }
}
