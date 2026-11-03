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

    private static Results<Created<ExampleResponse>, BadRequest> Create(CreateExampleRequest request, ExampleService service)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest();
        }

        Example example = service.Create(request.Name);
        return TypedResults.Created($"/default/examples/{example.Id}", new ExampleResponse(example.Id, example.Name));
    }

    private static Results<Ok<ExampleResponse>, NotFound> Get(Guid id, ExampleService service)
    {
        Example? example = service.Get(id);
        return example is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new ExampleResponse(example.Id, example.Name));
    }
}
