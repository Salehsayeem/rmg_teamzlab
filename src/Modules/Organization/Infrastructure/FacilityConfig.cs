using Microsoft.Extensions.DependencyInjection;
using Rmg.Modules.Organization.Application;
using Rmg.Modules.Organization.Contracts;

namespace Rmg.Modules.Organization.Infrastructure;

// ORG module wiring — the composition root Program.cs applies (W1 merge).
public static class FacilityConfig
{
    public static IServiceCollection AddOrganizationApi(this IServiceCollection services) =>
        services.AddScoped<IFacilityRepository, FacilityRepository>()
            .AddScoped<IOrganizationApi, FacilityService>();
}
