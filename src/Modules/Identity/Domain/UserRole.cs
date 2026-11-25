namespace Rmg.Modules.Identity.Domain;

// iam.user_role — user membership in a tenant role.
public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
