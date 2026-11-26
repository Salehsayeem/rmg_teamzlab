using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.ProductEngineering.Contracts;

namespace Rmg.Api.Controllers;

// Task 12 (PDM): style master + versions + variants. Reads need pdm.style.read, creation
// pdm.style.create, approval pdm.style.approve; the module API is ctor-injected from the DI
// composition root (PdmConfig.AddPdmApi, applied in Program.cs).
[ApiController]
[Authorize]
[Route("api/v1/pdm")]
public sealed class StylesController(IStyleApi api, ITenantContext tenant) : ControllerBase
{
    [HttpGet("styles")]
    [RequirePerm("pdm.style.read")]
    public async Task<IActionResult> Styles(CancellationToken ct) =>
        Ok(Envelope<StyleDto[]>.Of(await api.ListStylesAsync(tenant.TenantId!.Value, ct)));

    // Another tenant's id is filtered out — indistinguishable from a missing row: 404 NOT_FOUND.
    [HttpGet("styles/{id:guid}")]
    [RequirePerm("pdm.style.read")]
    public async Task<IActionResult> Style(Guid id, CancellationToken ct) =>
        Ok(Envelope<StyleDto>.Of(await api.FindStyleAsync(tenant.TenantId!.Value, id, ct)
            ?? throw new NotFoundProblem()));

    [HttpPost("styles")]
    [RequirePerm("pdm.style.create")]
    public async Task<IActionResult> CreateStyle([FromBody] StyleCreateRequest request, CancellationToken ct)
    {
        var style = await api.CreateStyleAsync(tenant.TenantId!.Value, Actor, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<StyleDto>.Of(style));
    }

    [HttpPost("styles/{id:guid}/approve")]
    [RequirePerm("pdm.style.approve")]
    public async Task<IActionResult> ApproveStyle(Guid id, CancellationToken ct) =>
        Ok(Envelope<StyleDto>.Of(await api.ApproveStyleAsync(tenant.TenantId!.Value, id, Actor, ct)));

    // TenantResolutionMiddleware guarantees tid/sub for authenticated requests.
    private string Actor => tenant.UserId?.ToString() ?? "pdm-api";
}
