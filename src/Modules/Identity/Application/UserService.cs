using Microsoft.AspNetCore.Identity;
using Rmg.BuildingBlocks.Common;
using Rmg.Modules.Identity.Contracts;
using Rmg.Modules.Identity.Domain;

namespace Rmg.Modules.Identity.Application;

// Persistence seam for users — implemented in module Infrastructure against the scoped tenant context.
public interface IUserRepository
{
    Task<List<AppUser>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<AppUser?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct);
    Task AddAsync(AppUser user, IReadOnlyList<Guid> roleIds, CancellationToken ct);
    Task<Dictionary<Guid, string[]>> RoleCodesByUserAsync(Guid tenantId, IReadOnlyList<Guid> userIds, CancellationToken ct);
    Task ReplaceRolesAsync(Guid tenantId, Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct);
}

public interface IRoleRepository
{
    Task<List<Role>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<List<Role>> FindByCodesAsync(Guid tenantId, IReadOnlyList<string> codes, CancellationToken ct);
}

// User management: creation hashes with PasswordHasher (the login verifier's counterpart), role codes
// resolve against the tenant's own catalogue — taken emails and unknown codes are 422s, never SQL errors.
public sealed class UserService(IUserRepository users, IRoleRepository roles)
{
    public async Task<UserDto> CreateAsync(Guid tenantId, string createdBy, UserCreateRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email)) errors["email"] = ["Email is required."];
        if (string.IsNullOrWhiteSpace(request.DisplayName)) errors["displayName"] = ["Display name is required."];
        if (string.IsNullOrEmpty(request.Password)) errors["password"] = ["Password is required."];
        if (errors.Count > 0) throw new ValidationProblem(errors);

        var email = request.Email.Trim();
        if (await users.EmailExistsAsync(tenantId, email, ct))
            throw new BusinessRuleProblem("EMAIL_TAKEN", $"Email '{email}' is already in use.");

        var assigned = await ResolveAsync(tenantId, request.RoleCodes, ct);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            Revision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, request.Password);

        await users.AddAsync(user, assigned.Select(r => r.Id).ToList(), ct);
        return ToDto(user, assigned);
    }

    public async Task<List<UserDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var all = await users.ListAsync(tenantId, ct);
        var rolesByUser = await users.RoleCodesByUserAsync(tenantId, all.Select(u => u.Id).ToList(), ct);
        return all.Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.IsActive, rolesByUser.GetValueOrDefault(u.Id, []))).ToList();
    }

    public async Task<UserDto> SetRolesAsync(Guid tenantId, Guid userId, string[]? roleCodes, CancellationToken ct = default)
    {
        var user = await users.FindAsync(tenantId, userId, ct)
            ?? throw new NotFoundProblem(); // unknown or other tenant's id — indistinguishable by design
        var assigned = await ResolveAsync(tenantId, roleCodes, ct);
        await users.ReplaceRolesAsync(tenantId, userId, assigned.Select(r => r.Id).ToList(), ct);
        return ToDto(user, assigned);
    }

    // Unknown codes are a per-field 422; an empty/omitted list legitimately means "no roles".
    private async Task<List<Role>> ResolveAsync(Guid tenantId, string[]? codes, CancellationToken ct)
    {
        codes ??= [];
        var wanted = codes.Distinct().ToList();
        var found = await roles.FindByCodesAsync(tenantId, wanted, ct);
        if (found.Count != wanted.Count)
            throw new RmgProblemException("ROLE_INVALID", "Unknown role codes.", 422,
                new Dictionary<string, string[]> { ["roleCodes"] = [.. wanted.Except(found.Select(r => r.Code))] });
        return found;
    }

    private static UserDto ToDto(AppUser user, IReadOnlyList<Role> assigned) =>
        new(user.Id, user.Email, user.DisplayName, user.IsActive, assigned.Select(r => r.Code).OrderBy(c => c).ToArray());
}
