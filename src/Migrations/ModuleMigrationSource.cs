using FluentMigrator.Runner.Initialization;
using System.Reflection;

namespace Migrations;

internal sealed class ModuleMigrationSource(Assembly assembly) : IMigrationSourceItem
{
    private readonly Assembly assembly = assembly;

    public IEnumerable<Type> MigrationTypeCandidates => assembly.GetTypes();
}
