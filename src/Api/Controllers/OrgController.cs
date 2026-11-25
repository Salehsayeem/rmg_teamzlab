using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Organization.Contracts;
using Rmg.Modules.Organization.Infrastructure;

namespace Rmg.Api.Controllers;

// ORG module endpoints. Program.cs is frozen during the W1 fan-out, so the controller composes the
// module API from the ambient scoped DbContext + ITenantContext via FacilityConfig.
[ApiController]
[Authorize]
[Route("api/v1/org")]
public sealed class OrgController(DbContext db, ITenantContext tenant) : ControllerBase
{
    private readonly IOrganizationApi _api = FacilityConfig.CreateApi(db, tenant);

    [HttpGet("facilities")]
    [RequirePerm("org.read")]
    public async Task<IActionResult> Facilities(CancellationToken ct) =>
        Ok(Envelope<FacilityDto[]>.Of(await _api.ListFacilitiesAsync(ct)));

    // Another tenant's id is filtered out — indistinguishable from a missing row: 404 NOT_FOUND.
    [HttpGet("facilities/{id:guid}")]
    [RequirePerm("org.read")]
    public async Task<IActionResult> Facility(Guid id, CancellationToken ct) =>
        Ok(Envelope<FacilityDto>.Of(await _api.FindFacilityAsync(id, ct) ?? throw new NotFoundProblem()));
}
