using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.Modules.Organization.Contracts;

namespace Rmg.Api.Controllers;

// ORG module endpoints — the module API is ctor-injected from the DI composition root
// (FacilityConfig.AddOrganizationApi, applied in Program.cs).
[ApiController]
[Authorize]
[Route("api/v1/org")]
public sealed class OrgController(IOrganizationApi api) : ControllerBase
{
    [HttpGet("facilities")]
    [RequirePerm("org.read")]
    public async Task<IActionResult> Facilities(CancellationToken ct) =>
        Ok(Envelope<FacilityDto[]>.Of(await api.ListFacilitiesAsync(ct)));

    // Another tenant's id is filtered out — indistinguishable from a missing row: 404 NOT_FOUND.
    [HttpGet("facilities/{id:guid}")]
    [RequirePerm("org.read")]
    public async Task<IActionResult> Facility(Guid id, CancellationToken ct) =>
        Ok(Envelope<FacilityDto>.Of(await api.FindFacilityAsync(id, ct) ?? throw new NotFoundProblem()));
}
