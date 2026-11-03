using System.Collections.Concurrent;
using Modules.Defaults.Application;
using Modules.Defaults.Domain;

namespace Modules.Defaults.Infrastructure;

// Demonstration only: data is lost when the service provider is disposed or the process stops.
internal sealed class InMemoryExampleRepository : IExampleRepository
{
    private readonly ConcurrentDictionary<Guid, Example> examples = new();

    public void Add(Example example) => examples[example.Id] = example;

    public Example? Get(Guid id) => examples.GetValueOrDefault(id);
}
