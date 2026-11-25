using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Database;
using Rmg.Modules.Identity.Application;
using Rmg.Modules.Identity.Contracts;
using Rmg.Modules.Identity.Infrastructure;

namespace Rmg.Api.Controllers;

// Task 9: IAM user & role management. Reads need iam.user.read, writes iam.user.manage; every query
// runs on the scoped tenant context, whose AppUser query filter pins users to the caller's tenant.
[ApiController]
[Authorize]
[Route("api/v1/iam")]
public sealed class IamController(TenantScopedDbContext db, ITenantContext tenant) : ControllerBase
{
    // The controller seam registers no module services — compose the module graph per request here.
    private UserService Users => new(new UserRepository(db), new RoleRepository(db));
    private RoleService Roles => new(new RoleRepository(db));

    [HttpGet("users")]
    [RequirePerm("iam.user.read")]
    public async Task<ActionResult<Envelope<List<UserDto>>>> ListUsers(CancellationToken ct) =>
        Ok(Envelope<List<UserDto>>.Of(await Users.ListAsync(tenant.TenantId!.Value, ct)));

    [HttpPost("users")]
    [RequirePerm("iam.user.manage")]
    public async Task<ActionResult<Envelope<UserDto>>> CreateUser([FromBody] UserCreateRequest request, CancellationToken ct)
    {
        var user = await Users.CreateAsync(tenant.TenantId!.Value, Actor, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<UserDto>.Of(user));
    }

    // Replaces the user's whole role set with the given codes (empty = no roles).
    [HttpPost("users/{id:guid}/roles")]
    [RequirePerm("iam.user.manage")]
    public async Task<ActionResult<Envelope<UserDto>>> SetUserRoles(Guid id, [FromBody] SetUserRolesRequest request, CancellationToken ct) =>
        Ok(Envelope<UserDto>.Of(await Users.SetRolesAsync(tenant.TenantId!.Value, id, request.RoleCodes, ct)));

    [HttpGet("roles")]
    [RequirePerm("iam.user.read")]
    public async Task<ActionResult<Envelope<List<RoleDto>>>> ListRoles(CancellationToken ct) =>
        Ok(Envelope<List<RoleDto>>.Of(await Roles.ListAsync(tenant.TenantId!.Value, ct)));

    // TenantResolutionMiddleware guarantees tid/sub for authenticated requests.
    private string Actor => tenant.UserId?.ToString() ?? "iam-api";
}
