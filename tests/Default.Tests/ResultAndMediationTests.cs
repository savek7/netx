using Microsoft.Extensions.DependencyInjection;
using Tools.Mediation.Abstractions;
using Tools.Mediation.MediatR;
using Tools.Results;
using Xunit;

namespace Modules.Defaults.Tests;

public sealed class ResultAndMediationTests
{
    [Fact]
    public void ResultWithoutValueEnforcesItsInvariants()
    {
        Error first = new("first", "First error", ErrorCategory.Validation);
        Error second = new("second", "Second error", ErrorCategory.Conflict);
        Error[] errors = [first, second];

        Result failure = Result.Failure(errors);
        errors[0] = second;

        Assert.True(failure.IsFailure);
        Assert.Equal([first, second], failure.Errors);
        Assert.Throws<ArgumentException>(() => Result.Failure());
        Assert.Throws<ArgumentException>(() => Result.Failure(first, null!));
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));

        Result success = Result.Success();
        Assert.True(success.IsSuccess);
        Assert.Empty(success.Errors);
    }

    [Fact]
    public void ResultsEnforceTheirInvariants()
    {
        Error first = new("first", "First error", ErrorCategory.Validation);
        Error second = new("second", "Second error", ErrorCategory.Conflict);
        Error[] errors = [first, second];
        Result<string> failure = Result<string>.Failure(errors);
        errors[0] = second;

        Assert.True(failure.IsFailure);
        Assert.Equal(first, failure.Errors[0]);
        Assert.Equal(second, failure.Errors[1]);
        Assert.Throws<InvalidOperationException>(() => failure.Value);
        Assert.Throws<ArgumentException>(() => Result<string>.Failure());
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));

        Result<string[]> success = Result<string[]>.Success([]);

        Assert.Empty(success.Value);
        Assert.Empty(success.Errors);
    }

    [Fact]
    public async Task AdapterPreservesResultsTokensAndExceptions()
    {
        ServiceCollection services = new();
        services.AddMediation();
        services.AddQueryHandler<TestQuery, TestHandler, string[]>();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope scope = provider.CreateScope();
        IQueryMediator mediator = scope.ServiceProvider.GetRequiredService<IQueryMediator>();
        using CancellationTokenSource source = new();
        TestQuery query = new();

        Result<string[]> success = await mediator.QueryAsync(query, source.Token);

        Assert.Empty(success.Value);
        Assert.Equal(source.Token, query.Token);

        query.Mode = 1;
        Assert.Equal("test.error", (await mediator.QueryAsync(query)).Errors[0].Code);

        query.Mode = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.QueryAsync(query));

        await source.CancelAsync();
        query.Mode = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mediator.QueryAsync(query, source.Token));
    }

    private sealed class TestQuery : IQuery<string[]>
    {
        public int Mode { get; set; }
        public CancellationToken Token { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by dependency injection.")]
    private sealed class TestHandler : IQueryHandler<TestQuery, string[]>
    {
        public Task<Result<string[]>> HandleAsync(TestQuery query, CancellationToken cancellationToken)
        {
            query.Token = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            if (query.Mode == 2)
            {
                throw new InvalidOperationException("technical failure");
            }

            return Task.FromResult(query.Mode == 1
                ? Result<string[]>.Failure(new Error("test.error", "Test error", ErrorCategory.BusinessRule))
                : Result<string[]>.Success([]));
        }
    }
}
