namespace Rmg.Modules.Organization.Domain;

// org.tenant_locale_policy — enabled locales per tenant, one flagged default.
public class TenantLocalePolicy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Locale { get; set; } = null!;
    public bool IsDefault { get; set; }

    // Baseline.
    public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
