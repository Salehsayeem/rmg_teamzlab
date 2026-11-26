using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Idempotency;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Merchandising.Contracts;

namespace Rmg.Api.Controllers;

// Task 14 (ORD/M005): customer order capture + lifecycle — the module API is ctor-injected from the
// DI composition root (MerchandisingConfig.AddMerchandisingApi, applied in Program.cs). Reads need
// ord.order.read, capture ord.order.create, confirm ord.order.confirm, cancel ord.order.edit.
[ApiController]
[Authorize]
[Route("api/v1/ord")]
public sealed class OrdersController(IOrderApi orders, ITenantContext tenant) : ControllerBase
{
    [HttpGet("customer-orders")]
    [RequirePerm("ord.order.read")]
    public async Task<IActionResult> Orders([FromQuery] int pageSize, [FromQuery] string? cursor, CancellationToken ct)
    {
        var (ordersList, meta) = await orders.ListOrdersAsync(tenant.TenantId!.Value, pageSize, cursor, ct);
        return Ok(Envelope<OrderDto[]>.Of(ordersList, meta));
    }

    // Another tenant's id is filtered out — indistinguishable from a missing row: 404 NOT_FOUND.
    [HttpGet("customer-orders/{id:guid}")]
    [RequirePerm("ord.order.read")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct) =>
        Ok(Envelope<OrderDto>.Of(await orders.FindOrderAsync(tenant.TenantId!.Value, id, ct)
            ?? throw new NotFoundProblem()));

    [HttpPost("customer-orders")]
    [RequirePerm("ord.order.create")]
    public async Task<IActionResult> Capture([FromBody] OrderCreateRequest request, CancellationToken ct)
    {
        var order = await orders.CaptureOrderAsync(tenant.TenantId!.Value, Actor, tenant.FacilityId, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<OrderDto>.Of(order));
    }

    // Confirm is the idempotent write: [Idempotent] marks it for the IdempotencyFilter (attached to
    // MVC actions in Program.cs), and the handler MUST return IResult — the filter only captures
    // IResult returns (contract comment in the filter; the probes use the same shape).
    [HttpPost("customer-orders/{id:guid}/confirm")]
    [Idempotent]
    [RequirePerm("ord.order.confirm")]
    public async Task<IResult> Confirm(Guid id, [FromBody] OrderConfirmRequest request, CancellationToken ct) =>
        Results.Json(Envelope<OrderDto>.Of(await orders.ConfirmOrderAsync(tenant.TenantId!.Value, id, Actor, request, ct)));

    [HttpPost("customer-orders/{id:guid}/cancel")]
    [RequirePerm("ord.order.edit")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] OrderCancelRequest request, CancellationToken ct) =>
        Ok(Envelope<OrderDto>.Of(await orders.CancelOrderAsync(tenant.TenantId!.Value, id, Actor, request, ct)));

    // TenantResolutionMiddleware guarantees tid/sub for authenticated requests.
    private string Actor => tenant.UserId?.ToString() ?? "ord-api";
}
