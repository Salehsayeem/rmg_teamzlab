using Rmg.Modules.Identity.Contracts;
using Rmg.Modules.Identity.Domain;

namespace Rmg.Modules.Identity.Application;

// Role catalogue reads for the caller's tenant (assignments go through UserService.SetRolesAsync).
public sealed class RoleService(IRoleRepository roles)
{
    public async Task<List<RoleDto>> ListAsync(Guid tenantId, CancellationToken ct = default) =>
        (await roles.ListAsync(tenantId, ct)).Select(r => new RoleDto(r.Id, r.Code)).ToList();
}
