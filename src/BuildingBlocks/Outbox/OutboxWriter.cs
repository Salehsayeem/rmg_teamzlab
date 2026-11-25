using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.BuildingBlocks.Outbox;

// Adds a Pending OutboxMessage to the ambient DbContext; persistence is the caller's SaveChanges/transaction.
public sealed class OutboxWriter(DbContext db, ITenantContext tenant, IHttpContextAccessor? httpAccessor) : IOutboxWriter
{
    public Task EnqueueAsync(string eventType, object payload, Guid aggregateId)
    {
        var eventId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var actor = tenant.UserId?.ToString() ?? "system";
        var correlationId = httpAccessor?.HttpContext?.Items[CorrelationMiddleware.CorrelationIdItemKey]?.ToString()
            ?? Guid.NewGuid().ToString(); // null-safe: works with no HTTP context (workers, tests)
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = eventId,
            EventType = eventType,
            SchemaVersion = 1,
            TenantId = tenant.TenantId
                ?? throw new InvalidOperationException("OutboxWriter: ITenantContext has no tenant set."),
            AggregateId = aggregateId,
            Actor = actor,
            CorrelationId = correlationId,
            OccurredAtUtc = now,
            RecordedAtUtc = now,
            Payload = JsonSerializer.Serialize(new
            {
                eventId,
                type = eventType,
                schemaVersion = 1,
                tenantId = tenant.TenantId,
                aggregateId,
                actor,
                correlationId,
                occurredAtUtc = now,
                data = JsonSerializer.SerializeToElement(payload),
            }),
            Status = "Pending",
            Attempts = 0,
            NextAttemptAtUtc = now,
        });
        return Task.CompletedTask;
    }
}
