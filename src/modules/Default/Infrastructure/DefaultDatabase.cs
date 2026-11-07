using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Tools.Modularity;

namespace Modules.Defaults.Infrastructure;

internal static class DefaultDatabase
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "PostgreSQL schema names use lowercase module IDs.")]
    public static string Schema => DefaultModule.Create().Id.ToLowerInvariant();

    public static void Configure(DbContextOptionsBuilder options, string connectionString, IModule module) =>
        options.UseNpgsql(connectionString, postgres => postgres.MigrationsHistoryTable(module.MigrationsTableName, Schema));
}

internal sealed class DefaultDbContextFactory : IDesignTimeDbContextFactory<DefaultDbContext>
{
    public DefaultDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Default for Default migrations.");
        DbContextOptionsBuilder<DefaultDbContext> options = new();
        DefaultDatabase.Configure(options, connectionString, DefaultModule.Create());
        return new DefaultDbContext(options.Options);
    }
}
