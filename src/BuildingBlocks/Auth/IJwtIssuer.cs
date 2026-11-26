using Rmg.Modules.Identity.Domain;

namespace Rmg.BuildingBlocks.Auth;

/// <summary>Issues signed access tokens for a user, embedding its permission codes.</summary>
public interface IJwtIssuer
{
    /// <returns>(token, expiresInSeconds) — expiry also encoded in the token's exp claim.</returns>
    Task<(string Token, int ExpiresInSeconds)> IssueAsync(AppUser user, IEnumerable<string> perms);
}
