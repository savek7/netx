using System.ComponentModel;
using System.Text.Json.Nodes;
using Modules.Defaults.Application;
using Modules.Defaults.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Modules.Defaults.Presentation;

internal readonly record struct CreateExampleRequest(string? Name);
internal sealed record ExampleResponse(Guid Id, string Name);

internal static class ExampleEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/default/examples");
        group.MapPost("/", Create)
            .WithSummary("Create an example")
            .WithDescription("Creates an example with a nonblank name. The response contains the created record and its Location header.")
            .AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
            {
                operation.RequestBody!.Content!["application/json"].Example = new JsonObject { ["name"] = "Example" };
                operation.Responses!["201"].Description = "Created; the Location header identifies the new record.";
                operation.Responses["400"].Description = "The name is blank or missing.";
                return Task.CompletedTask;
            });
        group.MapGet("/{id:guid}", Get)
            .WithSummary("Get an example")
            .WithDescription("Returns an example by its identifier.")
            .AddOpenApiOperationTransformer((operation, context, cancellationToken) =>
            {
                operation.Responses!["200"].Description = "The requested example.";
                operation.Responses["404"].Description = "No example exists with this identifier.";
                return Task.CompletedTask;
            });
    }

    private static async Task<Results<Created<ExampleResponse>, BadRequest>> Create(CreateExampleRequest request, ExampleService service, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest();
        }

        Example example = await service.CreateAsync(request.Name, cancellationToken);
        return TypedResults.Created($"/default/examples/{example.Id}", new ExampleResponse(example.Id, example.Name));
    }

    private static async Task<Results<Ok<ExampleResponse>, NotFound>> Get([Description("The GUID of the example to retrieve.")] Guid id, ExampleService service, CancellationToken cancellationToken)
    {
        Example? example = await service.GetAsync(id, cancellationToken);
        return example is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new ExampleResponse(example.Id, example.Name));
    }
}

