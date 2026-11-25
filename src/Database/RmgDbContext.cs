using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Idempotency;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Modules.Identity.Infrastructure;
using Rmg.Modules.Organization.Infrastructure;

namespace Rmg.Database;

// Single context composing module entity configurations (module Infrastructure assemblies; wired per-task).
public class RmgDbContext(DbContextOptions<RmgDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Compose the model from every module Infrastructure assembly + the BuildingBlocks ones.
        // Assemblies without configurations yet (Merchandising/ProductEngineering) are wired by their own tasks.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppUserConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboxMessageConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditEventConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyRecordConfiguration).Assembly);
    }
}
