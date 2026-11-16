using FluentMigrator.Runner;
using FluentMigrator.Runner.VersionTableInfo;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using FluentMigrator;
using Migrations;
using Microsoft.Extensions.DependencyInjection;
using Modules.Defaults;
using Npgsql;
using Tools.Modularity;

ModuleRegistration[] modules = [new(DefaultModule.Create(), typeof(DefaultModule).Assembly, "ConnectionStrings__Default")];
ModuleRegistration? selected = args.Length == 1
    ? Array.Find(modules, registration => string.Equals(registration.Module.Id, args[0], StringComparison.Ordinal))
    : null;
if (selected is null)
{
    await Console.Error.WriteLineAsync($"Specify a registered module: {string.Join(", ", modules.Select(registration => registration.Module.Id))}.");
    return 1;
}

string? connectionString = Environment.GetEnvironmentVariable(selected.ConnectionStringKey);
if (string.IsNullOrWhiteSpace(connectionString))
{
    await Console.Error.WriteLineAsync($"{selected.ConnectionStringKey} is required for {selected.Module.Id}.");
    return 1;
}

try
{
    NpgsqlConnectionStringBuilder migrationConnectionString = new(connectionString) { Pooling = false };
    ServiceCollection services = new();
    services.AddSingleton<IModule>(selected.Module);
    services.AddFluentMigratorCore()
        .ConfigureRunner(builder => builder.AddPostgres()
            .WithGlobalConnectionString(migrationConnectionString.ConnectionString))
        .AddLogging(builder => builder.AddFluentMigratorConsole());
    services.AddSingleton<IVersionTableMetaData, ModuleVersionTable>();
    services.AddSingleton<IMigrationSourceItem>(new ModuleMigrationSource(selected.Assembly));
    using ServiceProvider provider = services.BuildServiceProvider();
    using IServiceScope scope = provider.CreateScope();
    GenericProcessorBase processor = (GenericProcessorBase)scope.ServiceProvider.GetRequiredService<IMigrationProcessor>();
    using System.Data.IDbCommand lockCommand = processor.Connection.CreateCommand();
    lockCommand.CommandText = "SELECT pg_try_advisory_lock(hashtextextended(@name, 0))";
    System.Data.IDbDataParameter parameter = lockCommand.CreateParameter();
    parameter.ParameterName = "name";
    parameter.Value = $"netx:migrations:{selected.Module.Id}";
    lockCommand.Parameters.Add(parameter);
    if (lockCommand.ExecuteScalar() is not true)
    {
        await Console.Error.WriteLineAsync($"{selected.Module.Id} migration failed: another migration is already running for this module.");
        return 1;
    }
    scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    return 0;
}
#pragma warning disable CA1031 // Report all failures at the command-line boundary.
catch (Exception exception)
{
    await Console.Error.WriteLineAsync($"{selected.Module.Id} migration failed: {exception.Message}");
    return 1;
}

#pragma warning restore CA1031
