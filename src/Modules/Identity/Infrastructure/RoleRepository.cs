using Microsoft.EntityFrameworkCore;
using Rmg.Modules.Identity.Application;
using Rmg.Modules.Identity.Domain;

namespace Rmg.Modules.Identity.Infrastructure;

// EF reads over the tenant's role catalogue (roles are UNIQUE(tenant_id, code)).
public sealed class RoleRepository(DbContext db) : IRoleRepository
{
    public Task<List<Role>> ListAsync(Guid tenantId, CancellationToken ct) =>
        db.Set<Role>().Where(r => r.TenantId == tenantId).OrderBy(r => r.Code).ToListAsync(ct);

    public Task<List<Role>> FindByCodesAsync(Guid tenantId, IReadOnlyList<string> codes, CancellationToken ct) =>
        db.Set<Role>().Where(r => r.TenantId == tenantId && codes.Contains(r.Code)).ToListAsync(ct);
}
