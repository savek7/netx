using System.Reflection;
using Tools.Modularity;

namespace Migrations;

internal sealed record ModuleRegistration(IModule Module, Assembly Assembly, string ConnectionStringKey);
