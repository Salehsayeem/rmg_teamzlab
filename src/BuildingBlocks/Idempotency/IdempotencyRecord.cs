namespace Rmg.BuildingBlocks.Idempotency;

// iam.idempotency_record — stored write response keyed by tenant + endpoint + hashed Idempotency-Key.
public class IdempotencyRecord
{
    public Guid TenantId { get; set; }
    public string Endpoint { get; set; } = null!;
    public string KeyHash { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int ResponseStatus { get; set; }
    public string ResponseJson { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
