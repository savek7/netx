using Modules.Defaults.Domain;

namespace Modules.Defaults.Application;

internal interface IExampleRepository
{
    Task AddAsync(Example example, CancellationToken cancellationToken = default);
    Task<Example?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
