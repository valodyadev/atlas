using System.Security.Claims;
using Atlas.Modules.Identity.Application;
using Atlas.Modules.Identity.Contracts;
using Atlas.ServiceDefaults.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Atlas.Modules.Identity.Endpoints;

/// <summary>Registration, sign-in, token rotation and sign-out.</summary>
internal static class AuthEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints
            .MapGroup("/auth")
            .WithTags("Authentication")
            // Everything here must be reachable before the caller has a token.
            .AllowAnonymous();

        auth.MapPost("/register", async (
                AuthService service, RegisterRequest request, CancellationToken cancellationToken) =>
            {
                var result = await service.RegisterAsync(request, cancellationToken);
                return result.ToHttpResult(Results.Ok);
            })
            .WithName("Register")
            .WithSummary("Create an account.")
            .WithDescription(
                "Always reports the same acknowledgement, whether or not the address was "
                + "already registered, so the endpoint cannot be used to discover accounts.")
            .WithRequestValidation<RegisterRequest>()
            .Produces<AcknowledgementResponse>();

        auth.MapPost("/login", async (
                AuthService service, LoginRequest request, CancellationToken cancellationToken) =>
            {
                var result = await service.LoginAsync(request, cancellationToken);
                return result.Match(
                    tokens => Results.Ok(tokens),
                    _ => Results.Problem(
                        title: "Unauthorized",
                        detail: "Invalid email or password.",
                        statusCode: StatusCodes.Status401Unauthorized,
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "identity.auth.invalid_credentials",
                        }));
            })
            .WithName("Login")
            .WithSummary("Exchange an email and password for tokens.")
            .WithRequestValidation<LoginRequest>()
            .Produces<AuthTokensResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/refresh", async (
                AuthService service, RefreshRequest request, CancellationToken cancellationToken) =>
            {
                var result = await service.RefreshAsync(request.RefreshToken, cancellationToken);
                return result.Match(
                    tokens => Results.Ok(tokens),
                    _ => Results.Problem(
                        title: "Unauthorized",
                        detail: "Invalid email or password.",
                        statusCode: StatusCodes.Status401Unauthorized,
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "identity.auth.invalid_credentials",
                        }));
            })
            .WithName("Refresh")
            .WithSummary("Rotate a refresh token for a new pair.")
            .WithDescription(
                "Refresh tokens are single use. Presenting one that has already been "
                + "exchanged revokes every token in its family, on the assumption it was stolen.")
            .WithRequestValidation<RefreshRequest>()
            .Produces<AuthTokensResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", async (
                AuthService service, RefreshRequest request, CancellationToken cancellationToken) =>
            {
                var result = await service.LogoutAsync(request.RefreshToken, cancellationToken);
                return result.ToHttpResult(Results.Ok);
            })
            .WithName("Logout")
            .WithSummary("Revoke a refresh token and everything derived from it.")
            .WithRequestValidation<RefreshRequest>()
            .Produces<AcknowledgementResponse>();

        auth.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                if (!Guid.TryParse(id, out var userId))
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new CurrentUserResponse(
                    userId,
                    principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty,
                    principal.FindFirstValue(JwtRegisteredClaimNames.Name) ?? string.Empty,
                    principal.FindFirstValue(AtlasClaims.Role) ?? Domain.Roles.Member));
            })
            .WithName("CurrentUser")
            .WithSummary("The signed-in user, read from the access token.")
            .RequireAuthorization()
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
