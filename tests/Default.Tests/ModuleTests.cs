using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modules.Defaults;
using Modules.Defaults.Application;
using Modules.Defaults.Domain;
using Modules.Defaults.PublicApi;
using Tools.Modularity;
using Xunit;

namespace Modules.Defaults.Tests;

public sealed class ModuleTests
{
    [Fact]
    public async Task PublicApiCreatesAndReadsRecordsFromTheContainer()
    {
        IModule module = DefaultModule.Create();
        Assert.Equal("Default", module.Id);
        Assert.Equal("Migrations", module.MigrationsTableName);
        Assert.Empty(module.RequiredDependencies);
        ServiceCollection services = new();
        module.RegisterServices(services);
        services.AddSingleton<IExampleRepository>(new FakeExampleRepository());
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IExampleApi api = scope.ServiceProvider.GetRequiredService<IExampleApi>();

        ExampleRecord created = await api.CreateAsync("From module");

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(created, await api.FindByIdAsync(created.Id));
        Assert.Null(await api.FindByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => api.CreateAsync("  "));
    }

    [Fact]
    public async Task DevelopmentHttpAndPublicApiShareRecords()
    {
        await using WebApplicationFactory<Program> factory = new TestFactory("Development");
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage openApi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/default/examples", new { Name = "From HTTP" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        HttpExample? created = await response.Content.ReadFromJsonAsync<HttpExample>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("From HTTP", created.Name);
        Assert.NotNull(response.Headers.Location);
        HttpExample? fetched = await client.GetFromJsonAsync<HttpExample>(response.Headers.Location);
        Assert.Equal(created, fetched);
        using IServiceScope scope = factory.Services.CreateScope();
        IExampleApi api = scope.ServiceProvider.GetRequiredService<IExampleApi>();
        Assert.Equal(created.Name, (await api.FindByIdAsync(created.Id))?.Name);

        ExampleRecord fromApi = await api.CreateAsync("From API");
        HttpExample? fetchedFromApi = await client.GetFromJsonAsync<HttpExample>($"/default/examples/{fromApi.Id}");
        Assert.Equal(fromApi.Name, fetchedFromApi?.Name);
        using HttpResponseMessage missing = await client.GetAsync(new Uri($"/default/examples/{Guid.NewGuid()}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using HttpResponseMessage invalid = await client.PostAsJsonAsync("/default/examples", new { Name = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task ExampleEndpointsAreUnavailableOutsideDevelopment(string environment)
    {
        await using WebApplicationFactory<Program> factory = new TestFactory(environment);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage post = await client.PostAsJsonAsync("/default/examples", new { Name = "Example" });
        using HttpResponseMessage get = await client.GetAsync(new Uri($"/default/examples/{Guid.NewGuid()}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal("OK", await client.GetStringAsync(new Uri("/", UriKind.Relative)));
        using IServiceScope scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExampleApi>());
    }

    private sealed class TestFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExampleRepository>();
                services.AddSingleton<IExampleRepository>(new FakeExampleRepository());
            });
        }
    }

    private sealed class FakeExampleRepository : IExampleRepository
    {
        private readonly ConcurrentDictionary<Guid, Example> examples = new();
        public Task AddAsync(Example example, CancellationToken cancellationToken = default)
        {
            examples[example.Id] = example;
            return Task.CompletedTask;
        }
        public Task<Example?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(examples.GetValueOrDefault(id));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by JSON deserialization.")]
    private sealed record HttpExample(Guid Id, string Name);
}
