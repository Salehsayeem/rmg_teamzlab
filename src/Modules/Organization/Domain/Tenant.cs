namespace Rmg.Modules.Organization.Domain;

// org.tenant. Baseline columns are inline (P-2); the shared TenantEntity base arrives in Task 7.
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string DefaultLocale { get; set; } = null!;
    public bool RequireMakerChecker { get; set; }

    // Baseline (no tenant_id on the tenant itself).
    public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
