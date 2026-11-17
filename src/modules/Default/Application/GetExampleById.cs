using Modules.Defaults.Domain;
using Modules.Defaults.PublicApi;
using Tools.Mediation.Abstractions;
using Tools.Results;

namespace Modules.Defaults.Application;

internal sealed record GetExampleById(Guid Id) : IQuery<ExampleRecord>;

internal sealed class GetExampleByIdHandler(IExampleRepository repository) : IQueryHandler<GetExampleById, ExampleRecord>
{
    public async Task<Result<ExampleRecord>> HandleAsync(GetExampleById query, CancellationToken cancellationToken)
    {
        Example? example = await repository.GetAsync(query.Id, cancellationToken);

        return example is null
            ? Result<ExampleRecord>.Failure(ExampleErrors.NotFound)
            : Result<ExampleRecord>.Success(new ExampleRecord(example.Id, example.Name));
    }
}
