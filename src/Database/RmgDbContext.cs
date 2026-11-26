using Microsoft.EntityFrameworkCore;
using Rmg.BuildingBlocks.Audit;
using Rmg.BuildingBlocks.Idempotency;
using Rmg.BuildingBlocks.Outbox;
using Rmg.Modules.Identity.Infrastructure;
using Rmg.Modules.Merchandising.Infrastructure;
using Rmg.Modules.Organization.Infrastructure;
using Rmg.Modules.ProductEngineering.Infrastructure;

namespace Rmg.Database;

// Single context composing module entity configurations (module Infrastructure assemblies; wired per-task).
public class RmgDbContext(DbContextOptions<RmgDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Compose the model from every module Infrastructure assembly + the BuildingBlocks ones
        // (each assembly is wired additively by the task that first owns its configurations).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppUserConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuyerConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboxMessageConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditEventConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyRecordConfiguration).Assembly);
        // Task 11 (PDM): wired additively by its own task — controllers/configurations in module Infrastructure.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MaterialConfiguration).Assembly);
    }
}
