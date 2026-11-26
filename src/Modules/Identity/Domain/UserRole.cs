namespace Rmg.Modules.Identity.Domain;

// iam.user_role — user membership in a tenant role. tenant_id participates in the PK and both
// composite FKs (R-6): without RLS it is the constraint-level user↔role same-tenant guard.
public class UserRole
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
