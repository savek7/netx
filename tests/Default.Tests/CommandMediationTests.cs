using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Tools.Mediation.Abstractions;
using Tools.Mediation.MediatR;
using Tools.Results;
using Xunit;

namespace Modules.Defaults.Tests;

public sealed class CommandMediationTests
{
    [Fact]
    public async Task CommandWithValuePreservesResultsTokensAndExceptions()
    {
        using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandMediator mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();
        using CancellationTokenSource source = new();
        Error error = new("test.validation", "Invalid command", ErrorCategory.Validation);
        InvalidOperationException technicalFailure = new("technical failure");

        Result<string> success = await mediator.SendAsync(new ValueCommand(token =>
        {
            Assert.Equal(source.Token, token);
            return Result<string>.Success("created");
        }), source.Token);

        Assert.Equal("created", success.Value);
        Result<string> failure = await mediator.SendAsync(new ValueCommand(_ => Result<string>.Failure(error)));
        Assert.Equal(error, Assert.Single(failure.Errors));
        Assert.Same(technicalFailure, await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync(new ValueCommand(_ => throw technicalFailure))));

        await source.CancelAsync();
        OperationCanceledException canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.SendAsync(new ValueCommand(token =>
            {
                token.ThrowIfCancellationRequested();
                return Result<string>.Success("created");
            }), source.Token));

        Assert.Equal(source.Token, canceled.CancellationToken);
    }

    [Fact]
    public async Task CommandWithoutValuePreservesResultsTokensAndExceptions()
    {
        using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandMediator mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();
        using CancellationTokenSource source = new();
        Error first = new("test.validation", "Invalid command", ErrorCategory.Validation);
        Error second = new("test.conflict", "Conflicting command", ErrorCategory.Conflict);
        InvalidOperationException technicalFailure = new("technical failure");

        Result success = await mediator.SendAsync(new NoValueCommand(token =>
        {
            Assert.Equal(source.Token, token);
            return Result.Success();
        }), source.Token);

        Assert.True(success.IsSuccess);
        Result failure = await mediator.SendAsync(new NoValueCommand(_ => Result.Failure(first, second)));
        Assert.Equal([first, second], failure.Errors);
        Assert.Same(technicalFailure, await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync(new NoValueCommand(_ => throw technicalFailure))));

        await source.CancelAsync();
        OperationCanceledException canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.SendAsync(new NoValueCommand(token =>
            {
                token.ThrowIfCancellationRequested();
                return Result.Success();
            }), source.Token));

        Assert.Equal(source.Token, canceled.CancellationToken);
    }

    private static ServiceProvider CreateProvider()
    {
        ServiceCollection services = new();
        services.AddMediation();
        services.AddCommandHandler<ValueCommand, TestHandler, string>();
        services.AddCommandHandler<NoValueCommand, TestHandler>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed record ValueCommand(Func<CancellationToken, Result<string>> Execute) : ICommand<string>;

    private sealed record NoValueCommand(Func<CancellationToken, Result> Execute) : ICommand;

    [SuppressMessage("Performance", "CA1812", Justification = "Created by dependency injection.")]
    private sealed class TestHandler : ICommandHandler<ValueCommand, string>, ICommandHandler<NoValueCommand>
    {
        public Task<Result<string>> HandleAsync(ValueCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(command.Execute(cancellationToken));

        public Task<Result> HandleAsync(NoValueCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(command.Execute(cancellationToken));
    }
}
