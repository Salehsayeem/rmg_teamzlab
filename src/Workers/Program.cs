var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Workers.HeartbeatStub>();
// placeholder: replaced by OutboxDispatcher in BuildingBlocks.Outbox task
namespace Workers { public class HeartbeatStub : BackgroundService { protected override Task ExecuteAsync(CancellationToken t) => Task.Delay(Timeout.Infinite, t); } }
