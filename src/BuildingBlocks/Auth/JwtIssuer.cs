using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rmg.Modules.Identity.Domain;

namespace Rmg.BuildingBlocks.Auth;

/// <summary>Hand-rolled HS256 issuer (no extra NuGet dependency); validation stays with the
/// platform's JwtBearer handler. Claims: sub, tid, fid, name, perm (one per permission code).</summary>
public sealed class JwtIssuer(string signingKey) : IJwtIssuer
{
    public const string TokenIssuer = "rmg-api";
    public const string TokenAudience = "rmg-web";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public Task<(string Token, int ExpiresInSeconds)> IssueAsync(AppUser user, IEnumerable<string> perms)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(Lifetime);
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = TokenIssuer,
            ["aud"] = TokenAudience,
            ["sub"] = user.Id.ToString(),
            ["tid"] = user.TenantId.ToString(),
            ["name"] = user.DisplayName,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = expires.ToUnixTimeSeconds(),
            ["perm"] = perms.ToArray(),
        };

        var header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var body = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{header}.{body}";
        var signature = Base64Url.EncodeToString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.ASCII.GetBytes(signingInput)));

        return Task.FromResult(($"{signingInput}.{signature}", (int)Lifetime.TotalSeconds));
    }
}
