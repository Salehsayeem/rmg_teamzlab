using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Common;
using Rmg.BuildingBlocks.Tenancy;

namespace Rmg.BuildingBlocks.Audit;

// Adds an AuditEvent to the ambient DbContext; persistence is the caller's SaveChanges/transaction.
public sealed class AuditWriter(DbContext db, ITenantContext tenant, IHttpContextAccessor? httpAccessor) : IAuditWriter
{
    public Task WriteAsync(string command, string entityType, Guid entityId, object? before, object? after, string? reason = null)
    {
        var actor = tenant.UserId?.ToString() ?? "system";
        db.Set<AuditEvent>().Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId
                ?? throw new InvalidOperationException("AuditWriter: ITenantContext has no tenant set."),
            FacilityId = tenant.FacilityId,
            ActorId = actor,
            ActorDisplay = actor,
            Command = command,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            ChangedFields = Diff(before, after),
            Reason = reason,
            CorrelationId = httpAccessor?.HttpContext?.Items[CorrelationMiddleware.CorrelationIdItemKey]?.ToString()
                ?? Guid.NewGuid().ToString(), // null-safe: works with no HTTP context (workers, tests)
            OccurredAtUtc = DateTime.UtcNow,
        });
        return Task.CompletedTask;
    }

    // Reflection diff on same-named properties → {field: {before, after}} for every changed value.
    private static string Diff(object? before, object? after)
    {
        var oldValues = Props(before);
        var newValues = Props(after);
        var changed = new Dictionary<string, object?>();
        foreach (var name in oldValues.Keys.Union(newValues.Keys))
        {
            var oldVal = oldValues.GetValueOrDefault(name);
            var newVal = newValues.GetValueOrDefault(name);
            if (!Equals(oldVal, newVal))
                changed[name] = new { before = oldVal, after = newVal };
        }
        return JsonSerializer.Serialize(changed);
    }

    private static Dictionary<string, object?> Props(object? value) =>
        value?.GetType().GetProperties().ToDictionary(p => p.Name, p => (object?)p.GetValue(value)) ?? [];
}
