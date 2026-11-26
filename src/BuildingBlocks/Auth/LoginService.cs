using Microsoft.AspNetCore.Identity;
using Rmg.BuildingBlocks.Common;
using Rmg.Modules.Identity.Domain;

namespace Rmg.BuildingBlocks.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResult(string AccessToken, int ExpiresInSeconds, LoginUserSummary User);
public sealed record LoginUserSummary(Guid Id, string DisplayName);

/// <summary>Seam over the identity tables — implemented in the Database project (the DbContext
/// lives there; referencing it from here would close a project-graph cycle via Contracts).</summary>
public interface IAuthUserStore
{
    Task<AppUser?> FindActiveByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct);
}

// Verifies credentials and issues a JWT carrying the user's permission codes.
public sealed class LoginService(IAuthUserStore store, IJwtIssuer issuer)
{
    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        // pilot: login is email-only although uniqueness is (tenant_id, email) — single-tenant-per-email demo data.
        var user = await store.FindActiveByEmailAsync(email, ct);
        if (user is null)
            throw new UnauthorizedProblem("Invalid email or password.", Code: "INVALID_CREDENTIALS");

        var hasher = new PasswordHasher<AppUser>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw new UnauthorizedProblem("Invalid email or password.", Code: "INVALID_CREDENTIALS");

        var (token, expiresInSeconds) = await issuer.IssueAsync(user, await store.GetPermissionCodesAsync(user.Id, ct));
        return new LoginResult(token, expiresInSeconds, new LoginUserSummary(user.Id, user.DisplayName));
    }
}
