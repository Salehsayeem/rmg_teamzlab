namespace Rmg.BuildingBlocks.Idempotency;

/// <summary>Marks a POST endpoint as idempotent (see <see cref="IdempotencyFilter"/>, which gates on this
/// metadata). Minimal APIs register the filter itself via AddEndpointFilter&lt;IdempotencyFilter&gt;();
/// the attribute keeps the intent on the handler.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class IdempotentAttribute : Attribute;
