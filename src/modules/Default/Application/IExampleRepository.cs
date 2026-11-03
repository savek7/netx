using Modules.Defaults.Domain;

namespace Modules.Defaults.Application;

internal interface IExampleRepository
{
    void Add(Example example);
    Example? Get(Guid id);
}
