#pragma warning disable CA1812, S3011 // DI constructs adapters; reflection invokes only our own typed bridge.
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tools.Mediation.Abstractions;
using Tools.Results;

namespace Tools.Mediation.MediatR;

public static class MediationRegistration
{
    public static IServiceCollection AddMediation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(MediationRegistration).Assembly));
        services.TryAddScoped<IQueryMediator, QueryMediator>();

        return services;
    }

    public static IServiceCollection AddQueryHandler<TQuery, THandler, T>(this IServiceCollection services)
        where TQuery : IQuery<T>
        where THandler : class, IQueryHandler<TQuery, T>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<IRequestHandler<QueryRequest<T>, Result<T>>, QueryRequestHandler<T>>();
        services.AddScoped<IQueryHandler<TQuery, T>, THandler>();

        return services;
    }
}

internal sealed record QueryRequest<T>(IQuery<T> Query) : IRequest<Result<T>>;

internal sealed class QueryRequestHandler<T>(IServiceProvider services) : IRequestHandler<QueryRequest<T>, Result<T>>
{
    public Task<Result<T>> Handle(QueryRequest<T> request, CancellationToken cancellationToken)
    {
        Type handlerType = typeof(IQueryHandler<,>).MakeGenericType(request.Query.GetType(), typeof(T));

        try
        {
            return (Task<Result<T>>)typeof(QueryRequestHandler<T>)
                .GetMethod(nameof(Invoke), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(request.Query.GetType())
                .Invoke(null, [services.GetRequiredService(handlerType), request.Query, cancellationToken])!;
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static Task<Result<T>> Invoke<TQuery>(IQueryHandler<TQuery, T> handler, TQuery query, CancellationToken token)
        where TQuery : IQuery<T> => handler.HandleAsync(query, token);
}

internal sealed class QueryMediator(ISender sender) : IQueryMediator
{
    public Task<Result<T>> QueryAsync<T>(IQuery<T> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sender.Send(new QueryRequest<T>(query), cancellationToken);
    }
}
