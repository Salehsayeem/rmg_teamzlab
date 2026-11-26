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

/// <summary>Customer order capture + lifecycle (Task 14).</summary>
public interface IOrderApi
{
    Task<OrderDto> CaptureOrderAsync(Guid tenantId, string actor, Guid? facilityId, OrderCreateRequest request, CancellationToken ct = default);
    Task<(OrderDto[] Orders, PageMeta Meta)> ListOrdersAsync(Guid tenantId, int pageSize, string? cursor, CancellationToken ct = default);
    Task<OrderDto?> FindOrderAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<OrderDto> ConfirmOrderAsync(Guid tenantId, Guid orderId, string actor, OrderConfirmRequest request, CancellationToken ct = default);
    Task<OrderDto> CancelOrderAsync(Guid tenantId, Guid orderId, string actor, OrderCancelRequest request, CancellationToken ct = default);
}
