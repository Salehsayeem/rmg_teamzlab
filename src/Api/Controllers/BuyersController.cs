using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Merchandising.Contracts;

namespace Rmg.Api.Controllers;

// ORD module buyer profile endpoints (T10) — the module API is ctor-injected from the DI composition
// root (MerchandisingConfig.AddMerchandisingApi, applied in Program.cs). Reads need ord.buyer.read,
// creation ord.buyer.create; contact emails/phones are property-level authz: they leave the API only
// for holders of ord.buyer.manage, everyone else gets contacts: [{name}].
[ApiController]
[Authorize]
[Route("api/v1/ord")]
public sealed class BuyersController(IMerchandisingApi api, ITenantContext tenant) : ControllerBase
{
    [HttpGet("buyers")]
    [RequirePerm("ord.buyer.read")]
    public async Task<IActionResult> Buyers([FromQuery] int pageSize, [FromQuery] string? cursor, CancellationToken ct)
    {
        var (buyers, meta) = await api.ListBuyersAsync(tenant.TenantId!.Value, pageSize, cursor, ct);
        return Ok(Envelope<BuyerDto[]>.Of(Visible(buyers), meta));
    }

    [HttpPost("buyers")]
    [RequirePerm("ord.buyer.create")]
    public async Task<IActionResult> CreateBuyer([FromBody] BuyerCreateRequest request, CancellationToken ct)
    {
        var buyer = await api.CreateBuyerAsync(tenant.TenantId!.Value, Actor, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<BuyerDto>.Of(Visible(buyer)));
    }

    // Another tenant's id is filtered out — indistinguishable from a missing row: 404 NOT_FOUND.
    [HttpGet("buyers/{id:guid}")]
    [RequirePerm("ord.buyer.read")]
    public async Task<IActionResult> Buyer(Guid id, CancellationToken ct) =>
        Ok(Envelope<BuyerDto>.Of(Visible(await api.FindBuyerAsync(tenant.TenantId!.Value, id, ct)
            ?? throw new NotFoundProblem())));

    private bool CanSeeContactDetails =>
        User.HasClaim(c => c.Type == "perm" && c.Value == "ord.buyer.manage");

    private BuyerDto[] Visible(BuyerDto[] buyers) => CanSeeContactDetails ? buyers : [.. buyers.Select(Mask)];

    private BuyerDto Visible(BuyerDto buyer) => CanSeeContactDetails ? buyer : Mask(buyer);

    private static BuyerDto Mask(BuyerDto buyer) =>
        buyer with { Contacts = [.. buyer.Contacts.Select(c => c with { Email = null, Phone = null })] };

    // TenantResolutionMiddleware guarantees tid/sub for authenticated requests.
    private string Actor => tenant.UserId?.ToString() ?? "ord-api";
}
