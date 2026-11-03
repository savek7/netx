using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Defaults;
using Modules.Defaults.PublicApi;
using Tools.Modularity;
using Xunit;

namespace Modules.Defaults.Tests;

public sealed class ModuleTests
{
    [Fact]
    public void PublicApiCreatesAndReadsRecordsFromTheContainer()
    {
        IModule module = DefaultModule.Create();
        Assert.Equal("Default", module.Id);
        Assert.Empty(module.RequiredDependencies);
        ServiceCollection services = new();
        module.RegisterServices(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        IExampleApi api = provider.GetRequiredService<IExampleApi>();

        ExampleRecord created = api.Create("From module");

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(created, api.FindById(created.Id));
        Assert.Null(api.FindById(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => api.Create("  "));
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
        IExampleApi api = factory.Services.GetRequiredService<IExampleApi>();
        Assert.Equal(created.Name, api.FindById(created.Id)?.Name);

        ExampleRecord fromApi = api.Create("From API");
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
        Assert.NotNull(factory.Services.GetRequiredService<IExampleApi>());
    }

    [Fact]
    public void InMemoryDataIsNotSharedBetweenContainers()
    {
        ServiceCollection services = new();
        DefaultModule.Create().RegisterServices(services);
        using ServiceProvider first = services.BuildServiceProvider();
        using ServiceProvider second = services.BuildServiceProvider();
        ExampleRecord created = first.GetRequiredService<IExampleApi>().Create("Transient");
        Assert.Null(second.GetRequiredService<IExampleApi>().FindById(created.Id));
    }

    private sealed class TestFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(environment);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by JSON deserialization.")]
    private sealed record HttpExample(Guid Id, string Name);
}
