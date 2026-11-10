using FluentMigrator.Runner.Conventions;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.VersionTableInfo;
using Microsoft.Extensions.Options;
using Tools.Modularity;

namespace Migrations;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by dependency injection.")]
internal sealed class ModuleVersionTable(IConventionSet conventionSet, IOptions<RunnerOptions> options, IModule module)
    : DefaultVersionTableMetaData(conventionSet, options)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "PostgreSQL schema names use lowercase module IDs.")]
    public override string SchemaName => module.Id.ToLowerInvariant();
    public override string TableName => module.MigrationsTableName;
    public override bool OwnsSchema => true;
}
