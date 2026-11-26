namespace Rmg.Modules.Identity.Domain;

// iam.role_permission — role grant of a catalogue permission.
public class RolePermission
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = null!;
}
