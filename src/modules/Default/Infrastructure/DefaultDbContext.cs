using Microsoft.EntityFrameworkCore;
using Modules.Defaults.Domain;

namespace Modules.Defaults.Infrastructure;

internal sealed class DefaultDbContext(DbContextOptions<DefaultDbContext> options) : DbContext(options)
{
    public DbSet<Example> Examples => Set<Example>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultDatabase.Schema);
        modelBuilder.Entity<Example>(entity =>
        {
            entity.ToTable("Examples");
            entity.HasKey(example => example.Id);
            entity.Property(example => example.Id).ValueGeneratedNever();
            entity.Property(example => example.Name).IsRequired();
        });
    }
}
