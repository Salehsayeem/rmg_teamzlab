using Microsoft.Extensions.DependencyInjection;
using Rmg.Modules.Identity.Application;

namespace Rmg.Modules.Identity.Infrastructure;

// IAM module wiring — the composition root Program.cs applies (W1 merge), mirroring FacilityConfig.
public static class IamConfig
{
    public static IServiceCollection AddIamApi(this IServiceCollection services) =>
        services.AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IRoleRepository, RoleRepository>()
            .AddScoped<UserService>()
            .AddScoped<RoleService>();
}
