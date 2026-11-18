using Modules.Defaults.Domain;
using Modules.Defaults.PublicApi;
using Tools.Mediation.Abstractions;
using Tools.Results;

namespace Modules.Defaults.Application;

internal sealed record CreateExample(string? Name) : ICommand<ExampleRecord>;

internal sealed class CreateExampleHandler(IExampleRepository repository) : ICommandHandler<CreateExample, ExampleRecord>
{
    public async Task<Result<ExampleRecord>> HandleAsync(CreateExample command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<ExampleRecord>.Failure(ExampleErrors.InvalidName);
        }

        Example example = new(Guid.NewGuid(), command.Name);
        await repository.AddAsync(example, cancellationToken);

        return Result<ExampleRecord>.Success(new ExampleRecord(example.Id, example.Name));
    }
}
