namespace Rmg.Modules.Identity.Domain;

// iam.app_user — login identity (P-1: authored with M001; HTTP login lands in Task 4).
public class AppUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public bool IsActive { get; set; }

    // Baseline.
    public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
