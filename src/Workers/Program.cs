namespace Rmg.Workers;

// named class (not top-level statements) so the global-namespace `Program` doesn't collide with Api's
internal static class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<HeartbeatStub>();
        // placeholder: replaced by OutboxDispatcher in BuildingBlocks.Outbox task
    }
}

public class HeartbeatStub : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken t) => Task.Delay(Timeout.Infinite, t);
}
