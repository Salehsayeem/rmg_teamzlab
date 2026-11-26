using Rmg.BuildingBlocks.Common;

namespace Rmg.Modules.Merchandising.Contracts;

/// <summary>The Merchandising module's API surface as consumed by its controllers
/// (Controller → Service layering), mirroring IOrganizationApi.</summary>
public interface IMerchandisingApi
{
    Task<BuyerDto> CreateBuyerAsync(Guid tenantId, string actor, BuyerCreateRequest request, CancellationToken ct = default);
    Task<(BuyerDto[] Buyers, PageMeta Meta)> ListBuyersAsync(Guid tenantId, int pageSize, string? cursor, CancellationToken ct = default);
    Task<BuyerDto?> FindBuyerAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
