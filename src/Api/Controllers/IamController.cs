using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Identity.Application;
using Rmg.Modules.Identity.Contracts;

namespace Rmg.Api.Controllers;

// Task 9: IAM user & role management. Reads need iam.user.read, writes iam.user.manage; every query
// runs on the scoped tenant context, whose AppUser query filter pins users to the caller's tenant.
// Services are ctor-injected from the DI composition root (IamConfig.AddIamApi, applied in Program.cs).
[ApiController]
[Authorize]
[Route("api/v1/iam")]
public sealed class IamController(UserService users, RoleService roles, ITenantContext tenant) : ControllerBase
{
    [HttpGet("users")]
    [RequirePerm("iam.user.read")]
    public async Task<ActionResult<Envelope<List<UserDto>>>> ListUsers(CancellationToken ct) =>
        Ok(Envelope<List<UserDto>>.Of(await users.ListAsync(tenant.TenantId!.Value, ct)));

    [HttpPost("users")]
    [RequirePerm("iam.user.manage")]
    public async Task<ActionResult<Envelope<UserDto>>> CreateUser([FromBody] UserCreateRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(tenant.TenantId!.Value, Actor, request, ct);
        return StatusCode(StatusCodes.Status201Created, Envelope<UserDto>.Of(user));
    }

    // Replaces the user's whole role set with the given codes (empty = no roles).
    [HttpPost("users/{id:guid}/roles")]
    [RequirePerm("iam.user.manage")]
    public async Task<ActionResult<Envelope<UserDto>>> SetUserRoles(Guid id, [FromBody] SetUserRolesRequest request, CancellationToken ct) =>
        Ok(Envelope<UserDto>.Of(await users.SetRolesAsync(tenant.TenantId!.Value, id, request.RoleCodes, ct)));

    [HttpGet("roles")]
    [RequirePerm("iam.user.read")]
    public async Task<ActionResult<Envelope<List<RoleDto>>>> ListRoles(CancellationToken ct) =>
        Ok(Envelope<List<RoleDto>>.Of(await roles.ListAsync(tenant.TenantId!.Value, ct)));

    // TenantResolutionMiddleware guarantees tid/sub for authenticated requests.
    private string Actor => tenant.UserId?.ToString() ?? "iam-api";
}
