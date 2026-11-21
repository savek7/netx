using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Tools.Mediation.Abstractions;
using Tools.Mediation.MediatR;
using Tools.Results;
using Xunit;

namespace Modules.Defaults.Tests
{
    public sealed class MediationPipelineTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task QueryBehaviorsWrapTheHandlerInRegistrationOrder(bool isSuccess)
        {
            using CancellationTokenSource source = new();
            List<string> calls = [];
            Result<string> expected = isSuccess
                ? Result<string>.Success("found")
                : Result<string>.Failure(new Error("query.missing", "Missing record", ErrorCategory.NotFound));
            ServiceCollection services = CreateServices();
            services.AddScoped<IPipelineBehavior<TestQuery, Result<string>>>(_ =>
                new RecordingBehavior<TestQuery, Result<string>>("first", calls, source.Token));
            services.AddScoped<IPipelineBehavior<TestQuery, Result<string>>>(_ =>
                new RecordingBehavior<TestQuery, Result<string>>("second", calls, source.Token));
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();
            IQueryMediator mediator = scope.ServiceProvider.GetRequiredService<IQueryMediator>();

            Result<string> result = await mediator.QueryAsync(new TestQuery(token =>
            {
                Assert.Equal(source.Token, token);
                calls.Add("handler");
                return expected;
            }), source.Token);

            Assert.Same(expected, result);
            Assert.Equal(["first before", "second before", "handler", "second after", "first after"], calls);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CommandWithValueCanSkipTheRestOfThePipeline(bool isSuccess)
        {
            using CancellationTokenSource source = new();
            List<string> calls = [];
            Error invalid = new("command.invalid", "Invalid command", ErrorCategory.Validation);
            Error conflict = new("command.conflict", "Conflicting command", ErrorCategory.Conflict);
            Result<string> expected = isSuccess ? Result<string>.Success("resolved") : Result<string>.Failure(invalid, conflict);
            ServiceCollection services = CreateServices();
            services.AddScoped<IPipelineBehavior<ValueCommand, Result<string>>>(_ =>
                new RecordingBehavior<ValueCommand, Result<string>>("first", calls, source.Token));
            services.AddScoped<IPipelineBehavior<ValueCommand, Result<string>>>(_ =>
                new ReturningBehavior<ValueCommand, Result<string>>(expected, calls, source.Token));
            services.AddScoped<IPipelineBehavior<ValueCommand, Result<string>>>(_ =>
                new RecordingBehavior<ValueCommand, Result<string>>("last", calls, source.Token));
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();
            ICommandMediator mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();

            Result<string> result = await mediator.SendAsync(new ValueCommand(_ => throw new InvalidOperationException("Handler ran")), source.Token);

            Assert.Equal(["first before", "returned", "first after"], calls);
            if (isSuccess)
            {
                Assert.Equal("resolved", result.Value);
            }
            else
            {
                Assert.True(result.IsFailure);
                Assert.Equal([invalid, conflict], result.Errors);
            }
        }

        [Fact]
        public async Task CommandWithoutValuePreservesResultsExceptionsAndCancellationThroughBehavior()
        {
            using CancellationTokenSource source = new();
            List<string> calls = [];
            ServiceCollection services = CreateServices();
            services.AddScoped<IPipelineBehavior<NoValueCommand, Result>>(_ =>
                new RecordingBehavior<NoValueCommand, Result>("command", calls, source.Token));
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using IServiceScope scope = provider.CreateScope();
            ICommandMediator mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();

            Result success = await mediator.SendAsync(new NoValueCommand(token =>
            {
                Assert.Equal(source.Token, token);
                calls.Add("handler");
                return Result.Success();
            }), source.Token);

            Assert.True(success.IsSuccess);
            Assert.Equal(["command before", "handler", "command after"], calls);
            calls.Clear();

            Result expectedFailure = Result.Failure(new Error("command.blocked", "Command blocked", ErrorCategory.BusinessRule));
            Assert.Same(expectedFailure, await mediator.SendAsync(new NoValueCommand(_ => expectedFailure), source.Token));

            InvalidOperationException technicalFailure = new("Technical failure");
            Assert.Same(technicalFailure, await Assert.ThrowsAsync<InvalidOperationException>(
                () => mediator.SendAsync(new NoValueCommand(_ => throw technicalFailure), source.Token)));

            await source.CancelAsync();
            OperationCanceledException canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => mediator.SendAsync(new NoValueCommand(token =>
                {
                    token.ThrowIfCancellationRequested();
                    return Result.Success();
                }), source.Token));

            Assert.Equal(source.Token, canceled.CancellationToken);
            Assert.Equal(["command before", "command after", "command before", "command before"], calls);
        }

        private static ServiceCollection CreateServices()
        {
            ServiceCollection services = new();
            services.AddMediation();
            services.AddQueryHandler<TestQuery, TestHandler, string>();
            services.AddCommandHandler<ValueCommand, TestHandler, string>();
            services.AddCommandHandler<NoValueCommand, TestHandler>();
            return services;
        }

        private sealed record TestQuery(Func<CancellationToken, Result<string>> Execute) : IQuery<string>;
        private sealed record ValueCommand(Func<CancellationToken, Result<string>> Execute) : ICommand<string>;
        private sealed record NoValueCommand(Func<CancellationToken, Result> Execute) : ICommand;

        [SuppressMessage("Performance", "CA1812", Justification = "Created by dependency injection.")]
        private sealed class TestHandler : IQueryHandler<TestQuery, string>, ICommandHandler<ValueCommand, string>, ICommandHandler<NoValueCommand>
        {
            public Task<Result<string>> HandleAsync(TestQuery query, CancellationToken cancellationToken)
            {
                return Task.FromResult(query.Execute(cancellationToken));
            }

            public Task<Result<string>> HandleAsync(ValueCommand command, CancellationToken cancellationToken)
            {
                return Task.FromResult(command.Execute(cancellationToken));
            }

            public Task<Result> HandleAsync(NoValueCommand command, CancellationToken cancellationToken)
            {
                return Task.FromResult(command.Execute(cancellationToken));
            }
        }

        private sealed class RecordingBehavior<TMessage, TResult>(string name, List<string> calls, CancellationToken expectedToken)
            : IPipelineBehavior<TMessage, TResult>
        {
            public async Task<TResult> HandleAsync(TMessage message, PipelineNext<TResult> invokeNext, CancellationToken cancellationToken)
            {
                Assert.Equal(expectedToken, cancellationToken);
                calls.Add($"{name} before");
                TResult result = await invokeNext();
                calls.Add($"{name} after");
                return result;
            }
        }

        private sealed class ReturningBehavior<TMessage, TResult>(TResult result, List<string> calls, CancellationToken expectedToken)
            : IPipelineBehavior<TMessage, TResult>
        {
            public Task<TResult> HandleAsync(TMessage message, PipelineNext<TResult> invokeNext, CancellationToken cancellationToken)
            {
                Assert.Equal(expectedToken, cancellationToken);
                calls.Add("returned");
                return Task.FromResult(result);
            }
        }
    }
}