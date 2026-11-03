namespace Modules.Defaults.PublicApi;

public interface IExampleApi
{
    ExampleRecord Create(string name);
    ExampleRecord? FindById(Guid id);
}

public sealed record ExampleRecord(Guid Id, string Name);
