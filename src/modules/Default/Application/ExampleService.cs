using Modules.Defaults.Domain;

namespace Modules.Defaults.Application;

internal sealed class ExampleService(IExampleRepository repository)
{
    public async Task<Example> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Example example = new(Guid.NewGuid(), name);
        await repository.AddAsync(example, cancellationToken);
        return example;
    }

    public Task<Example?> GetAsync(Guid id, CancellationToken cancellationToken = default) => repository.GetAsync(id, cancellationToken);
}
