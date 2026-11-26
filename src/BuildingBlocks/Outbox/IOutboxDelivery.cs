namespace Rmg.BuildingBlocks.Outbox;

/// <summary>Delivery port the dispatcher drives per message — real transports (webhooks, module
/// handlers) implement this; the pilot delivers as a structured log line.</summary>
public interface IOutboxDelivery
{
    Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
