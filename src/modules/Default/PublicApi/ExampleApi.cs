using Modules.Defaults.Application;
using Modules.Defaults.Domain;

namespace Modules.Defaults.PublicApi;

internal sealed class ExampleApi(ExampleService service) : IExampleApi
{
    public ExampleRecord Create(string name) => ToRecord(service.Create(name));

    public ExampleRecord? FindById(Guid id)
    {
        Example? example = service.Get(id);
        return example is null ? null : ToRecord(example);
    }

    private static ExampleRecord ToRecord(Example example) => new(example.Id, example.Name);
}
