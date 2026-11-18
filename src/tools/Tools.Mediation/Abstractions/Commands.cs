#pragma warning disable CA1040, S2326 // Command markers identify supported messages and their success data.
using Tools.Results;

namespace Tools.Mediation.Abstractions;

public interface ICommand<T> { }

public interface ICommand { }

public interface ICommandHandler<in TCommand, T> where TCommand : ICommand<T>
{
    Task<Result<T>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandMediator
{
    Task<Result<T>> SendAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default);
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default);
}
