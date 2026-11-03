using Modules.Defaults.Domain;

namespace Modules.Defaults.Application;

internal sealed class ExampleService(IExampleRepository repository)
{
    public Example Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Example example = new(Guid.NewGuid(), name);
        repository.Add(example);
        return example;
    }

    public Example? Get(Guid id) => repository.Get(id);
}
