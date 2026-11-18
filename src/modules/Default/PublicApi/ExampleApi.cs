using Modules.Defaults.Application;
using Tools.Mediation.Abstractions;
using Tools.Results;

namespace Modules.Defaults.PublicApi;

internal sealed class ExampleApi(ICommandMediator commandMediator, IQueryMediator queryMediator) : IExampleApi
{
    public Task<Result<ExampleRecord>> CreateAsync(string? name, CancellationToken cancellationToken = default) =>
        commandMediator.SendAsync(new CreateExample(name), cancellationToken);

    public Task<Result<ExampleRecord>> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        queryMediator.QueryAsync(new GetExampleById(id), cancellationToken);
}
