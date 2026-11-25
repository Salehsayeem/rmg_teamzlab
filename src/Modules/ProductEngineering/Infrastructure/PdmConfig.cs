using Microsoft.Extensions.DependencyInjection;
using Rmg.Modules.ProductEngineering.Application;
using Rmg.Modules.ProductEngineering.Contracts;

namespace Rmg.Modules.ProductEngineering.Infrastructure;

// PDM module wiring — the composition root Program.cs applies, mirroring IamConfig/FacilityConfig.
public static class PdmConfig
{
    public static IServiceCollection AddPdmApi(this IServiceCollection services) =>
        services.AddScoped<IMaterialRepository, MaterialRepository>()
            .AddScoped<IPdmApi, MaterialService>();
}
