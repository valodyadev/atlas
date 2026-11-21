using FluentValidation;

namespace Atlas.Modules.Identity.Contracts;

/// <summary>Request body for creating a user.</summary>
/// <param name="Email">Email address.</param>
/// <param name="DisplayName">Name shown in the UI.</param>
public sealed record CreateUserRequest(string Email, string DisplayName);

/// <summary>A user as exposed over HTTP.</summary>
/// <param name="Id">Stable identity.</param>
/// <param name="Email">Normalised email address.</param>
/// <param name="DisplayName">Name shown in the UI.</param>
/// <param name="CreatedAt">When the account was created.</param>
public sealed record UserResponse(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

/// <summary>
/// Shape validation for <see cref="CreateUserRequest"/>.
/// </summary>
/// <remarks>
/// This rejects obviously malformed input at the edge and produces a good 400.
/// It is not a substitute for the domain invariants in
/// <see cref="Domain.User.Register"/>, which are the actual source of truth -
/// the API is only one of several possible callers.
/// </remarks>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    /// <summary>Defines the rules.</summary>
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(Domain.User.EmailMaxLength)
            .EmailAddress();

        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(Domain.User.DisplayNameMaxLength);
    }
}
