using FluentMigrator.Runner.Initialization;
using System.Reflection;

namespace Migrations;

internal sealed class ModuleMigrationSource(Assembly assembly) : IMigrationSourceItem
{
    public IEnumerable<Type> MigrationTypeCandidates => assembly.GetTypes();
}
