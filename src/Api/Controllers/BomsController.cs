using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.ProductEngineering.Contracts;

namespace Rmg.Api.Controllers;

// Task 13 (PDM): BOM authoring — create per style version, add lines while Draft (pdm.bom.edit),
// release (pdm.bom.approve); the module API is ctor-injected from the DI composition root
// (PdmConfig.AddPdmApi, applied in Program.cs).
[ApiController]
[Authorize]
[Route("api/v1/pdm")]
public sealed class BomsController(IBomApi api, ITenantContext tenant) : ControllerBase
{
    [HttpGet("boms")]
    [RequirePerm("pdm.bom.read")]
    public async Task<IActionResult> Boms(CancellationToken ct) =>
        Ok(Envelope<BomDto[]>.Of(await api.ListBomsAsync(tenant.TenantId!.Value, ct)));

    // Another tenant's id is filtered out — indistinguishable from a missing row: 404 NOT_FOUND.
    [HttpGet("boms/{id:guid}")]
    [RequirePerm("pdm.bom.read")]
    public async Task<IActionResult> Bom(Guid id, CancellationToken ct) =>
        Ok(Envelope<BomDto>.Of(await api.FindBomAsync(tenant.TenantId!.Value, id, ct)
            ?? throw new NotFoundProblem()));

    [HttpPost("boms")]
    [RequirePerm("pdm.bom.create")]
    public async Task<IActionResult> CreateBom([FromBody] BomCreateRequest request, CancellationToken ct)
    {
        var bom = await api.CreateBomAsync(tenant.TenantId!.Value, Actor, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<BomDto>.Of(bom));
    }

    [HttpPost("boms/{id:guid}/lines")]
    [RequirePerm("pdm.bom.edit")]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] BomLineAddRequest request, CancellationToken ct)
    {
        var line = await api.AddBomLineAsync(tenant.TenantId!.Value, id, Actor, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<BomLineDto>.Of(line));
    }

    [HttpPost("boms/{id:guid}/approve")]
    [RequirePerm("pdm.bom.approve")]
    public async Task<IActionResult> ApproveBom(Guid id, CancellationToken ct) =>
        Ok(Envelope<BomDto>.Of(await api.ApproveBomAsync(tenant.TenantId!.Value, id, Actor, ct)));

    // TenantResolutionMiddleware guarantees tid/sub for authenticated requests.
    private string Actor => tenant.UserId?.ToString() ?? "pdm-api";
}
