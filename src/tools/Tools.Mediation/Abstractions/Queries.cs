#pragma warning disable CA1040, S2326 // Typed query marker.
using Tools.Results;

namespace Tools.Mediation.Abstractions;

public interface IQuery<T> { }

public interface IQueryHandler<in TQuery, T> where TQuery : IQuery<T>
{
    Task<Result<T>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface IQueryMediator
{
    Task<Result<T>> QueryAsync<T>(IQuery<T> query, CancellationToken cancellationToken = default);
}
