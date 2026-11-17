using Modules.Defaults.Application;
using Modules.Defaults.Domain;
using Tools.Mediation.Abstractions;
using Tools.Results;

namespace Modules.Defaults.PublicApi;

internal sealed class ExampleApi(ExampleService service, IQueryMediator mediator) : IExampleApi
{
    public async Task<ExampleRecord> CreateAsync(string name, CancellationToken cancellationToken = default) =>
        ToRecord(await service.CreateAsync(name, cancellationToken));

    public Task<Result<ExampleRecord>> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        mediator.QueryAsync(new GetExampleById(id), cancellationToken);

    private static ExampleRecord ToRecord(Example example) => new(example.Id, example.Name);
}
