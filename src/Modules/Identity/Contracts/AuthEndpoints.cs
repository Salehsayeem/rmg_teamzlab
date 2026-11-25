using Microsoft.AspNetCore.Builder;
using Rmg.BuildingBlocks.Auth;
using Rmg.BuildingBlocks.Common;

namespace Rmg.Modules.Identity.Contracts;

// R-2 login endpoint; verification and token issuance live in BuildingBlocks/Auth (LoginService).
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app) =>
        app.MapPost("/api/v1/auth/login",
                async (LoginRequest request, LoginService login, CancellationToken ct) =>
                    Envelope<LoginResult>.Of(await login.LoginAsync(request.Email, request.Password, ct)))
            .RequireRateLimiting("login");
}
