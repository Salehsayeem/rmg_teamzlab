using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Database;
using Rmg.Workers;

// named class (not top-level statements) so the global-namespace `Program` doesn't collide with Api's
internal static class Program
{
    public static async Task Main(string[] args)
    {
        // Walked up from cwd because dotnet run doesn't guarantee cwd = repo root (same as the Database CLI).
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, ".env"))) continue;
            DotNetEnv.Env.Load(Path.Combine(dir.FullName, ".env"));
            break;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddDbContext<RmgDbContext>(o =>
            o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
        builder.Services.AddScoped<IOutboxDelivery, OutboxLogDelivery>();
        builder.Services.AddHostedService<OutboxDispatcher>();

        await builder.Build().RunAsync();
    }
}

// Pilot delivery: a structured log line per message until real transports land (per-module tasks).
public sealed class OutboxLogDelivery(ILogger<OutboxLogDelivery> log) : IOutboxDelivery
{
    public Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        log.LogInformation(
            "Outbox delivery (pilot): {EventType} {EventId} tenant {TenantId} aggregate {AggregateId}",
            message.EventType, message.Id, message.TenantId, message.AggregateId);
        return Task.CompletedTask;
    }
}
