using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Rmg.BuildingBlocks.Common;

namespace Rmg.BuildingBlocks.Auth;

// R-2 login endpoint; credential verification is LoginService, mapped from the Api's auth block.
public static class AuthEndpointsMap
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/login",
                async (LoginRequest request, LoginService login, CancellationToken ct) =>
                    Envelope<LoginResult>.Of(await login.LoginAsync(request.Email, request.Password, ct)))
            .RequireRateLimiting("login");
}
