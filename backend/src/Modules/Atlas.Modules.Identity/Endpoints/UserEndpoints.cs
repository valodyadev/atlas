using Atlas.Modules.Identity.Application;
using Atlas.Modules.Identity.Contracts;
using Atlas.ServiceDefaults.ErrorHandling;
using Atlas.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Atlas.Modules.Identity.Endpoints;

/// <summary>HTTP surface of the Identity module.</summary>
internal static class UserEndpoints
{
    /// <summary>Maps the user endpoints.</summary>
    /// <param name="endpoints">Route builder, already versioned.</param>
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var users = endpoints
            .MapGroup("/users")
            .WithTags("Users")
            // Reading the user list requires a signed-in caller. Creation now
            // lives at /auth/register.
            .RequireAuthorization();

        users.MapGet("/", async (
                UserService service,
                int? limit,
                string? cursor,
                CancellationToken cancellationToken) =>
            {
                var page = await service.ListAsync(PageRequest.Of(limit, cursor), cancellationToken);
                return Results.Ok(page);
            })
            .WithName("ListUsers")
            .WithSummary("List users, newest first.")
            .Produces<Page<UserResponse>>();

        users.MapGet("/{id:guid}", async (
                UserService service,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetAsync(id, cancellationToken);
                return result.ToHttpResult(Results.Ok);
            })
            .WithName("GetUser")
            .WithSummary("Fetch a single user by id.")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    }
}
