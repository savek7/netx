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
        group.MapPost("/", Create);
        group.MapGet("/{id:guid}", Get);
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

    private static async Task<Results<Ok<ExampleResponse>, NotFound>> Get(Guid id, ExampleService service, CancellationToken cancellationToken)
    {
        Example? example = await service.GetAsync(id, cancellationToken);
        return example is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new ExampleResponse(example.Id, example.Name));
    }
}
