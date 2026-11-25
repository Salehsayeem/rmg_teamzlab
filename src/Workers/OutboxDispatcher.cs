using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Database;

namespace Rmg.Workers;

// Polls outbox.outbox_message for due Pending rows and drives them through IOutboxDelivery.
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    ILogger<OutboxDispatcher> log) : BackgroundService
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 10;
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueBatchAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }

    // Public so tests (and a future triggered mode) can run one batch without the poll loop.
    public async Task ProcessDueBatchAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RmgDbContext>();
        var delivery = scope.ServiceProvider.GetRequiredService<IOutboxDelivery>();

        var now = DateTime.UtcNow;
        var batch = await db.Set<OutboxMessage>()
            .Where(m => m.Status == "Pending" && m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.NextAttemptAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in batch)
        {
            try
            {
                await delivery.DeliverAsync(message, ct);
                message.Status = "Processed";
                log.LogInformation("Outbox delivered {EventType} {EventId} for aggregate {AggregateId}",
                    message.EventType, message.Id, message.AggregateId);
            }
            catch (Exception ex)
            {
                message.Attempts++;
                if (message.Attempts >= MaxAttempts)
                {
                    message.Status = "Failed";
                    log.LogError(ex, "OUTBOX_DEAD_LETTER {EventType} {EventId} after {Attempts} attempts",
                        message.EventType, message.Id, message.Attempts);
                }
                else
                {
                    message.NextAttemptAtUtc = DateTime.UtcNow + Backoff(message.Attempts);
                    log.LogWarning(ex, "Outbox delivery failed {EventType} {EventId}, attempt {Attempts}, retry at {NextAttemptAtUtc}",
                        message.EventType, message.Id, message.Attempts, message.NextAttemptAtUtc);
                }
            }
        }

        if (batch.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    // Retry spec: next_attempt = UtcNow + 60s · 2^attempts, capped at 15 minutes.
    internal static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(60d * Math.Pow(2, attempts), 900d));
}
