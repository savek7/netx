using Tools.Results;

namespace Modules.Defaults.PublicApi;

public interface IExampleApi
{
    Task<ExampleRecord> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<Result<ExampleRecord>> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ExampleRecord(Guid Id, string Name);
