using Modules.Defaults.Application;
using Modules.Defaults.Domain;

namespace Modules.Defaults.PublicApi;

internal sealed class ExampleApi(ExampleService service) : IExampleApi
{
    public async Task<ExampleRecord> CreateAsync(string name, CancellationToken cancellationToken = default) =>
        ToRecord(await service.CreateAsync(name, cancellationToken));

    public async Task<ExampleRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Example? example = await service.GetAsync(id, cancellationToken);
        return example is null ? null : ToRecord(example);
    }

    private static ExampleRecord ToRecord(Example example) => new(example.Id, example.Name);
}
