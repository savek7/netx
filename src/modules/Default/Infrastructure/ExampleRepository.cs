using Microsoft.EntityFrameworkCore;
using Modules.Defaults.Application;
using Modules.Defaults.Domain;

namespace Modules.Defaults.Infrastructure;

internal sealed class ExampleRepository(DefaultDbContext context) : IExampleRepository
{
    public async Task AddAsync(Example example, CancellationToken cancellationToken = default)
    {
        context.Examples.Add(example);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Example?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Examples.AsNoTracking().SingleOrDefaultAsync(example => example.Id == id, cancellationToken);
}
