using Microsoft.EntityFrameworkCore;
using Modules.Defaults.Domain;

namespace Modules.Defaults.Infrastructure;

internal sealed class DefaultDbContext(DbContextOptions<DefaultDbContext> options) : DbContext(options)
{
    public DbSet<Example> Examples => Set<Example>();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "PostgreSQL schema names use lowercase module IDs.")]
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultModule.Create().Id.ToLowerInvariant());
        modelBuilder.Entity<Example>(entity =>
        {
            entity.ToTable("Examples");
            entity.HasKey(example => example.Id);
            entity.Property(example => example.Id).ValueGeneratedNever();
            entity.Property(example => example.Name).IsRequired();
        });
    }
}
