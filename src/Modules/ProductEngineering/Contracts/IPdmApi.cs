namespace Rmg.Modules.ProductEngineering.Contracts;

/// <summary>The PDM module's API surface as consumed by its controllers (Controller → Service layering).</summary>
public interface IPdmApi
{
    Task<UomDto[]> ListUomsAsync(CancellationToken ct = default);
    Task<MaterialDto[]> ListMaterialsAsync(CancellationToken ct = default);
    Task<MaterialDto> CreateMaterialAsync(Guid tenantId, string createdBy, MaterialCreateRequest request, CancellationToken ct = default);
}
