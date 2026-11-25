using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rmg.BuildingBlocks.Outbox;
using Rmg.BuildingBlocks.Tenancy;
using Rmg.Database;
using Rmg.Workers;

namespace Rmg.IntegrationTests.BuildingBlocks;

// Outbox acceptance: the writer enqueues a Pending row with the envelope payload, and one dispatcher
// batch processes due rows — retry reschedules below the attempt cap, and attempt 10 dead-letters.
public class OutboxTests : IntegrationTestBase
{
    [Fact]
    public async Task Dispatcher_ProcessesPending_AndDeadLettersAfter10()
    {
        var tenant = new TenantContext();
        tenant.Set(Guid.NewGuid(), null, Guid.NewGuid());
        var aggregateId = Guid.NewGuid();

        // -- writer: enqueue into the ambient DbContext (no SaveChanges inside the writer)
        var writer = new OutboxWriter(Db, tenant, httpAccessor: null);
        await writer.EnqueueAsync("pdm.StylePublished", new { StyleCode = "ST-1" }, aggregateId);
        Assert.Empty(await Db.Set<OutboxMessage>().ToListAsync()); // still just a change-set entry
        await Db.SaveChangesAsync();

        await using var readEnqueued = FreshContext();
        var enqueued = await readEnqueued.Set<OutboxMessage>().SingleAsync();
        Assert.Equal("Pending", enqueued.Status);
        Assert.Equal("pdm.StylePublished", enqueued.EventType);
        Assert.Equal(1, enqueued.SchemaVersion);
        Assert.Equal(tenant.TenantId!.Value, enqueued.TenantId);
        Assert.Equal(aggregateId, enqueued.AggregateId);
        Assert.Equal(tenant.UserId!.Value.ToString(), enqueued.Actor);
        Assert.Equal(enqueued.OccurredAtUtc, enqueued.RecordedAtUtc);

        // Envelope wire format: {eventId, type, schemaVersion, tenantId, aggregateId, actor,
        // correlationId, occurredAtUtc, data} — data is the payload, not a double-encoded string.
        var envelope = JsonDocument.Parse(enqueued.Payload).RootElement;
        foreach (var key in new[]
                 {
                     "eventId", "type", "schemaVersion", "tenantId", "aggregateId",
                     "actor", "correlationId", "occurredAtUtc", "data",
                 })
            Assert.True(envelope.TryGetProperty(key, out _), $"envelope missing {key}");
        Assert.Equal("ST-1", envelope.GetProperty("data").GetProperty("StyleCode").GetString());

        // -- dispatcher: one due Pending row is delivered and marked Processed
        var delivered = new List<Guid>();
        var logger = new ListLogger();
        await using (var provider = BuildProvider(msg =>
                     {
                         delivered.Add(msg.Id);
                         return Task.CompletedTask;
                     }))
        {
            await RunBatch(provider, logger);
        }
        Assert.Equal([enqueued.Id], delivered);
        await using (var processedDb = FreshContext())
            Assert.Equal("Processed", (await processedDb.Set<OutboxMessage>().SingleAsync(m => m.Id == enqueued.Id)).Status);

        // -- dispatcher: below the cap a failure reschedules (60s · 2^attempts, capped 15 min); at 10 it dead-letters
        var reschedule = PendingRow("retry.me", attempts: 8);
        var deadLetter = PendingRow("give.up", attempts: 9);
        Db.AddRange(reschedule, deadLetter);
        await Db.SaveChangesAsync();

        await using (var provider = BuildProvider(_ => throw new InvalidOperationException("pilot delivery down")))
        {
            await RunBatch(provider, logger);
        }

        await using var readBack = FreshContext();
        var retried = await readBack.Set<OutboxMessage>().SingleAsync(m => m.Id == reschedule.Id);
        Assert.Equal(9, retried.Attempts);
        Assert.Equal("Pending", retried.Status); // 60s · 2^9 = 30720s → capped at 900s
        Assert.True(retried.NextAttemptAtUtc > DateTime.UtcNow.AddMinutes(14));

        var failed = await readBack.Set<OutboxMessage>().SingleAsync(m => m.Id == deadLetter.Id);
        Assert.Equal(10, failed.Attempts);
        Assert.Equal("Failed", failed.Status);

        Assert.Contains(logger.Messages, m => m.Contains("OUTBOX_DEAD_LETTER"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("OUTBOX_DEAD_LETTER") && m.Contains("retry.me"));
    }

    private async Task RunBatch(ServiceProvider provider, ListLogger logger)
    {
        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(), logger);
        await dispatcher.ProcessDueBatchAsync();
    }

    private ServiceProvider BuildProvider(Func<OutboxMessage, Task> onDeliver)
    {
        var services = new ServiceCollection();
        services.AddDbContext<RmgDbContext>(o => o.UseNpgsql(ConnectionString));
        services.AddScoped<IOutboxDelivery>(_ => new StubDelivery(onDeliver));
        return services.BuildServiceProvider();
    }

    // Own connection, no shared change tracker — read-backs assert what Postgres actually stored.
    private RmgDbContext FreshContext() =>
        new(new DbContextOptionsBuilder<RmgDbContext>().UseNpgsql(ConnectionString).Options);

    private OutboxMessage PendingRow(string eventType, int attempts) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        SchemaVersion = 1,
        TenantId = Guid.NewGuid(),
        AggregateId = Guid.NewGuid(),
        Actor = "system",
        CorrelationId = "test",
        OccurredAtUtc = DateTime.UtcNow,
        RecordedAtUtc = DateTime.UtcNow,
        Payload = "{}",
        Status = "Pending",
        Attempts = attempts,
        NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1),
    };

    private sealed class StubDelivery(Func<OutboxMessage, Task> onDeliver) : IOutboxDelivery
    {
        public Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken = default)
            => onDeliver(message);
    }

    private sealed class ListLogger : ILogger<OutboxDispatcher>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}

