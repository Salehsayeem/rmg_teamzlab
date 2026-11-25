namespace Rmg.Modules.Organization.Domain;

// org.facility.
public class Facility
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LegalEntityId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Timezone { get; set; } = null!;
    public string? Address { get; set; }

    // Baseline.
    public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
