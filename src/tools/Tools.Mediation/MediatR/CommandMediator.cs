#pragma warning disable CA1812, S3011 // DI constructs the command adapters.
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Tools.Mediation.Abstractions;
using Tools.Results;

namespace Tools.Mediation.MediatR;

internal sealed class CommandMediator(ISender sender) : ICommandMediator
{
    public Task<Result<T>> SendAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return sender.Send(new CommandRequest<T>(command), cancellationToken);
    }

    public Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return sender.Send(new CommandRequest(command), cancellationToken);
    }
}

internal sealed record CommandRequest<T>(ICommand<T> Command) : IRequest<Result<T>>;

internal sealed record CommandRequest(ICommand Command) : IRequest<Result>;

internal sealed class CommandRequestHandler<T>(IServiceProvider services) : IRequestHandler<CommandRequest<T>, Result<T>>
{
    public Task<Result<T>> Handle(CommandRequest<T> request, CancellationToken cancellationToken)
    {
        Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(request.Command.GetType(), typeof(T));

        try
        {
            return (Task<Result<T>>)typeof(CommandRequestHandler<T>)
                .GetMethod(nameof(Invoke), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(request.Command.GetType())
                .Invoke(null, [services.GetRequiredService(handlerType), request.Command, cancellationToken])!;
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static Task<Result<T>> Invoke<TCommand>(ICommandHandler<TCommand, T> handler, TCommand command, CancellationToken token)
        where TCommand : ICommand<T> => handler.HandleAsync(command, token);
}

internal sealed class CommandRequestHandler(IServiceProvider services) : IRequestHandler<CommandRequest, Result>
{
    public Task<Result> Handle(CommandRequest request, CancellationToken cancellationToken)
    {
        Type handlerType = typeof(ICommandHandler<>).MakeGenericType(request.Command.GetType());

        try
        {
            return (Task<Result>)typeof(CommandRequestHandler)
                .GetMethod(nameof(Invoke), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(request.Command.GetType())
                .Invoke(null, [services.GetRequiredService(handlerType), request.Command, cancellationToken])!;
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static Task<Result> Invoke<TCommand>(ICommandHandler<TCommand> handler, TCommand command, CancellationToken token)
        where TCommand : ICommand => handler.HandleAsync(command, token);
}
