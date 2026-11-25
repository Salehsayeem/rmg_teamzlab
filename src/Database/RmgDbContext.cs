using Microsoft.EntityFrameworkCore;

namespace Rmg.Database;

// Single context composing module entity configurations (module Infrastructure assemblies; wired per-task).
public class RmgDbContext(DbContextOptions<RmgDbContext> options) : DbContext(options);
