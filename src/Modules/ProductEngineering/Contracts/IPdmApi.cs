namespace Rmg.Modules.ProductEngineering.Contracts;

/// <summary>The PDM module's API surface as consumed by its controllers (Controller → Service layering).</summary>
public interface IPdmApi
{
    Task<UomDto[]> ListUomsAsync(CancellationToken ct = default);
    Task<MaterialDto[]> ListMaterialsAsync(CancellationToken ct = default);
    Task<MaterialDto> CreateMaterialAsync(Guid tenantId, string createdBy, MaterialCreateRequest request, CancellationToken ct = default);
}

/// <summary>Style master + version/variant authoring (Task 12).</summary>
public interface IStyleApi
{
    Task<StyleDto[]> ListStylesAsync(Guid tenantId, CancellationToken ct = default);
    Task<StyleDto?> FindStyleAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<StyleDto> CreateStyleAsync(Guid tenantId, string createdBy, StyleCreateRequest request, CancellationToken ct = default);
    Task<StyleDto> ApproveStyleAsync(Guid tenantId, Guid styleId, string actor, CancellationToken ct = default);
}

/// <summary>BOM authoring — create, add lines while Draft, release (Task 13).</summary>
public interface IBomApi
{
    Task<BomDto[]> ListBomsAsync(Guid tenantId, CancellationToken ct = default);
    Task<BomDto?> FindBomAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<BomDto> CreateBomAsync(Guid tenantId, string createdBy, BomCreateRequest request, CancellationToken ct = default);
    Task<BomLineDto> AddBomLineAsync(Guid tenantId, Guid bomId, string actor, BomLineAddRequest request, CancellationToken ct = default);
    Task<BomDto> ApproveBomAsync(Guid tenantId, Guid bomId, string actor, CancellationToken ct = default);
}
