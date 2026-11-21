using FluentValidation;

namespace Atlas.Modules.Identity.Contracts;

/// <summary>Request body for registration.</summary>
public sealed record RegisterRequest(string Email, string Password, string DisplayName);

/// <summary>Request body for sign-in.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Request body carrying a refresh token.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>A freshly issued credential pair.</summary>
/// <param name="AccessToken">Short-lived bearer token.</param>
/// <param name="RefreshToken">Opaque token, valid once, used to get the next pair.</param>
/// <param name="ExpiresIn">Seconds until the access token expires.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
public sealed record AuthTokensResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType = "Bearer");

/// <summary>A deliberately uninformative acknowledgement.</summary>
public sealed record AcknowledgementResponse(string Message);

/// <summary>The signed-in user's own profile.</summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, string Role);

/// <summary>
/// Shape validation for registration.
/// </summary>
/// <remarks>
/// Length is the only password rule. Composition rules ("one symbol, one
/// digit") push people towards predictable substitutions and are no longer
/// recommended; a long passphrase beats a short complex one.
/// </remarks>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(Options.IdentityAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(request => request.Email)
            .NotEmpty().MaximumLength(Domain.User.EmailMaxLength).EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(options.MinimumPasswordLength)
                .WithMessage($"Password must be at least {options.MinimumPasswordLength} characters.")
            .MaximumLength(256);

        RuleFor(request => request.DisplayName)
            .NotEmpty().MaximumLength(Domain.User.DisplayNameMaxLength);
    }
}

/// <summary>Shape validation for sign-in. Deliberately lax: the generic failure does the work.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(Domain.User.EmailMaxLength);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(256);
    }
}

/// <summary>Shape validation for refresh.</summary>
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(request => request.RefreshToken).NotEmpty().MaximumLength(512);
}
