using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.ProductEngineering.Contracts;

namespace Rmg.Api.Controllers;

// PDM: material master + UOM reference data. Materials are tenant-scoped (the context-rooted query
// filter pins rows to the caller's tenant); UOMs are global reference data readable by any
// authenticated user (no RequirePerm). The module API is ctor-injected from the DI composition root
// (PdmConfig.AddPdmApi, applied in Program.cs).
[ApiController]
[Authorize]
[Route("api/v1/pdm")]
public sealed class MaterialsController(IPdmApi api, ITenantContext tenant) : ControllerBase
{
    [HttpGet("uoms")]
    public async Task<IActionResult> Uoms(CancellationToken ct) =>
        Ok(Envelope<UomDto[]>.Of(await api.ListUomsAsync(ct)));

    [HttpGet("materials")]
    [RequirePerm("pdm.material.read")]
    public async Task<IActionResult> Materials(CancellationToken ct) =>
        Ok(Envelope<MaterialDto[]>.Of(await api.ListMaterialsAsync(ct)));

    [HttpPost("materials")]
    [RequirePerm("pdm.material.create")]
    public async Task<IActionResult> CreateMaterial([FromBody] MaterialCreateRequest request, CancellationToken ct)
    {
        var material = await api.CreateMaterialAsync(tenant.TenantId!.Value, Actor, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<MaterialDto>.Of(material));
    }

    // TenantResolutionMiddleware guarantees tid/sub for authenticated requests.
    private string Actor => tenant.UserId?.ToString() ?? "pdm-api";
}
