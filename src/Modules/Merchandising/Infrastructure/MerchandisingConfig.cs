using Microsoft.Extensions.DependencyInjection;
using Rmg.Modules.Merchandising.Application;
using Rmg.Modules.Merchandising.Contracts;

namespace Rmg.Modules.Merchandising.Infrastructure;

// Merchandising module wiring — the composition root Program.cs applies (T10), mirroring IamConfig.
public static class MerchandisingConfig
{
    public static IServiceCollection AddMerchandisingApi(this IServiceCollection services) =>
        services.AddScoped<IBuyerRepository, BuyerRepository>()
            .AddScoped<IMerchandisingApi, BuyerService>();
}
