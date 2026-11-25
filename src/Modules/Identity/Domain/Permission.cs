namespace Rmg.Modules.Identity.Domain;

// iam.permission — global catalogue (module.resource.action); code is the key.
public class Permission
{
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
}
