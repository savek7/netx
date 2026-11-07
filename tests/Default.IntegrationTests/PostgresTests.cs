using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Defaults.Infrastructure;
using Modules.Defaults.PublicApi;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Modules.Defaults.IntegrationTests;

public sealed class PostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:17").Build();

    public Task InitializeAsync() => postgres.StartAsync();
    public async Task DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task MigrationsAndBothInterfacesPersistAcrossHostRestart()
    {
        string connectionString = postgres.GetConnectionString();
        Guid httpId;
        Guid apiId;

        await using (TestFactory factory = new(connectionString))
        {
            using HttpClient client = factory.CreateClient();
            // Starting the host must not apply migrations.
            await using (NpgsqlConnection connection = new(connectionString))
            {
                await connection.OpenAsync();
                await using NpgsqlCommand command = new("SELECT to_regclass('default.\"Examples\"')", connection);
                Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync());
            }

            using (IServiceScope migrationScope = factory.Services.CreateScope())
            {
                DefaultDbContext context = migrationScope.ServiceProvider.GetRequiredService<DefaultDbContext>();
                await context.Database.MigrateAsync();
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            }

            await using (NpgsqlConnection connection = new(connectionString))
            {
                await connection.OpenAsync();
                await using NpgsqlCommand command = new("SELECT count(*) FROM \"default\".\"Migrations\"", connection);
                Assert.Equal(1L, await command.ExecuteScalarAsync());
            }

            using HttpResponseMessage response = await client.PostAsJsonAsync("/default/examples", new { Name = "From HTTP" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            HttpExample? created = await response.Content.ReadFromJsonAsync<HttpExample>();
            Assert.NotNull(created);
            httpId = created.Id;
            Assert.NotNull(response.Headers.Location);
            Assert.Equal(created, await client.GetFromJsonAsync<HttpExample>(response.Headers.Location));

            using IServiceScope scope = factory.Services.CreateScope();
            IExampleApi api = scope.ServiceProvider.GetRequiredService<IExampleApi>();
            Assert.Equal(created.Name, (await api.FindByIdAsync(httpId))?.Name);
            apiId = (await api.CreateAsync("From API")).Id;
            Assert.Equal("From API", (await client.GetFromJsonAsync<HttpExample>($"/default/examples/{apiId}"))?.Name);
        }

        await using (TestFactory restarted = new(connectionString))
        {
            using HttpClient client = restarted.CreateClient();
            Assert.Equal("From HTTP", (await client.GetFromJsonAsync<HttpExample>($"/default/examples/{httpId}"))?.Name);
            using IServiceScope scope = restarted.Services.CreateScope();
            IExampleApi api = scope.ServiceProvider.GetRequiredService<IExampleApi>();
            Assert.Equal("From API", (await api.FindByIdAsync(apiId))?.Name);
            Assert.Equal("From HTTP", (await api.FindByIdAsync(httpId))?.Name);
        }
    }

    private sealed class TestFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
            }));
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by JSON deserialization.")]
    private sealed record HttpExample(Guid Id, string Name);
}
