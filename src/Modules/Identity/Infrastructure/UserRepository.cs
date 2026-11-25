using Microsoft.EntityFrameworkCore;
using Rmg.Modules.Identity.Application;
using Rmg.Modules.Identity.Domain;

namespace Rmg.Modules.Identity.Infrastructure;

// EF persistence for IAM — resolved instance is the scoped TenantScopedDbContext, so the AppUser
// query filter pins reads to the caller's tenant on top of the explicit tenantId predicates below.
public sealed class UserRepository(DbContext db) : IUserRepository
{
    public Task<List<AppUser>> ListAsync(Guid tenantId, CancellationToken ct) =>
        db.Set<AppUser>().Where(u => u.TenantId == tenantId).OrderBy(u => u.Email).ToListAsync(ct);

    public Task<AppUser?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Set<AppUser>().SingleOrDefaultAsync(u => u.TenantId == tenantId && u.Id == id, ct);

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct) =>
        db.Set<AppUser>().AnyAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    // One SaveChanges: the user row and its same-tenant role links commit atomically (R-1).
    public async Task AddAsync(AppUser user, IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        db.Add(user);
        db.AddRange(roleIds.Select(roleId => new UserRole { TenantId = user.TenantId, UserId = user.Id, RoleId = roleId }));
        await db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<Guid, string[]>> RoleCodesByUserAsync(Guid tenantId, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var rows = await db.Set<UserRole>().Where(ur => ur.TenantId == tenantId && userIds.Contains(ur.UserId))
            .Join(db.Set<Role>().Where(r => r.TenantId == tenantId), ur => ur.RoleId, r => r.Id,
                (ur, r) => new { ur.UserId, r.Code })
            .ToListAsync(ct);
        return rows.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).Distinct().OrderBy(c => c).ToArray());
    }

    public async Task ReplaceRolesAsync(Guid tenantId, Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        var current = await db.Set<UserRole>().Where(ur => ur.TenantId == tenantId && ur.UserId == userId).ToListAsync(ct);
        var target = roleIds.ToHashSet();
        db.RemoveRange(current.Where(ur => !target.Contains(ur.RoleId)));
        db.AddRange(target.Except(current.Select(ur => ur.RoleId))
            .Select(roleId => new UserRole { TenantId = tenantId, UserId = userId, RoleId = roleId }));
        await db.SaveChangesAsync(ct);
    }
}
