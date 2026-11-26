using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Auth;
using Rmg.Modules.Identity.Domain;

namespace Rmg.Database;

// Login queries for LoginService — the DbContext lives in this project.
public sealed class AuthUserStore(RmgDbContext db) : IAuthUserStore
{
    public Task<AppUser?> FindActiveByEmailAsync(string email, CancellationToken ct) =>
        db.Set<AppUser>().FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct) =>
        await db.Set<UserRole>().Where(ur => ur.UserId == userId)
            .Join(db.Set<RolePermission>(), ur => ur.RoleId, rp => rp.RoleId, (_, rp) => rp.PermissionCode)
            .Distinct()
            .ToListAsync(ct);
}
