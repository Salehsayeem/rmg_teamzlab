using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rmg.Modules.Identity.Domain;

namespace Rmg.Modules.Identity.Infrastructure;

// Baseline columns are mapped inline per entity (P-2); the shared TenantEntity base arrives in Task 7.
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("app_user", "iam");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Email).HasColumnName("email");
        b.Property(x => x.PasswordHash).HasColumnName("password_hash");
        b.Property(x => x.DisplayName).HasColumnName("display_name");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("role", "iam");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Code).HasColumnName("code");
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.Property(x => x.Revision).HasColumnName("revision").HasDefaultValue(1);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}

// Global catalogue: no tenant scoping, code is the key.
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permission", "iam");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasColumnName("code");
        b.Property(x => x.Description).HasColumnName("description");
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permission", "iam");
        b.HasKey(x => new { x.RoleId, x.PermissionCode });
        b.Property(x => x.RoleId).HasColumnName("role_id");
        b.Property(x => x.PermissionCode).HasColumnName("permission_id");
        b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId);
        b.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionCode);
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_role", "iam");

        // R-6: tenant_id inside the PK and both composite FKs — a user and their role must
        // belong to the same tenant, enforced at the constraint level (no RLS in the pilot).
        b.HasKey(x => new { x.TenantId, x.UserId, x.RoleId });
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.RoleId).HasColumnName("role_id");
        b.HasOne<AppUser>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UserId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
        b.HasOne<Role>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RoleId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id });
    }
}
