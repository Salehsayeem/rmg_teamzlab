using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Modules.Organization.Application;
using Rmg.Modules.Organization.Contracts;

namespace Rmg.Modules.Organization.Infrastructure;

// ORG module wiring.
public static class FacilityConfig
{
    // DI registration — the additive Program.cs block the W1 integration merge applies
    // (Program.cs is frozen during the parallel fan-out).
    public static IServiceCollection AddOrganizationApi(this IServiceCollection services) =>
        services.AddScoped<IFacilityRepository, FacilityRepository>()
            .AddScoped<IOrganizationApi, FacilityService>();

    // Composition used until that registration lands: the host injects the ambient scoped DbContext
    // + ITenantContext and composes the module API itself.
    public static IOrganizationApi CreateApi(DbContext db, ITenantContext tenant) =>
        new FacilityService(new FacilityRepository(db, tenant));
}
