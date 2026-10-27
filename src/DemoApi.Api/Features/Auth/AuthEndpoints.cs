


using AuthPolicies = DemoApi.BuildingBlocks.Security.Policies;

namespace DemoApi.Api.Features.Auth;

// EP-01
public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }
}

public sealed class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.Password).Password();
        RuleFor(r => r.FirstName).Name();
        RuleFor(r => r.LastName).Name();
        RuleFor(r => r.Phone).Phone();
    }
}

public sealed class RegisterEndpoint : Endpoint<RegisterRequest, AcceptedResponse>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Register a customer account";
            s.Description = "Creates an unconfirmed user, the Customer role and a customer record. Always returns 202 with the same message, so it does not reveal whether the email exists.";
        });
    }

    public override async Task HandleAsync(RegisterRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new RegisterCommand(r.Email.Trim(), r.Password, r.FirstName, r.LastName, r.Phone).ExecuteAsync(ct)).ToAccepted(HttpContext));
}

// EP-02
public sealed class ConfirmEmailRequest
{
    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;
}

public sealed class ConfirmEmailValidator : Validator<ConfirmEmailRequest>
{
    public ConfirmEmailValidator()
    {
        RuleFor(r => r.UserId).NotEmpty();
        RuleFor(r => r.Token).NotEmpty().MaximumLength(Rules.TokenMax);
    }
}

public sealed class ConfirmEmailEndpoint : Endpoint<ConfirmEmailRequest>
{
    public override void Configure()
    {
        Post("/auth/confirm-email");
        AllowAnonymous();
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Confirm an email address";
            s.Description = "Confirms the email with the token from the confirmation link. Returns 204, also when already confirmed, and a generic 400 invalid-token otherwise.";
        });
    }

    public override async Task HandleAsync(ConfirmEmailRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new ConfirmEmailCommand(r.UserId, r.Token).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-03, EP-10
public sealed class EmailRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class EmailRequestValidator : Validator<EmailRequest>
{
    public EmailRequestValidator() => RuleFor(r => r.Email).Email();
}

public sealed class ResendConfirmationEndpoint : Endpoint<EmailRequest, AcceptedResponse>
{
    public override void Configure()
    {
        Post("/auth/resend-confirmation");
        AllowAnonymous();
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Resend the confirmation email";
            s.Description = "Sends a new confirmation link for an unconfirmed account. Always returns 202 with the same message.";
        });
    }

    public override async Task HandleAsync(EmailRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new ResendConfirmationCommand(r.Email.Trim()).ExecuteAsync(ct)).ToAccepted(HttpContext));
}

public sealed class ForgotPasswordEndpoint : Endpoint<EmailRequest, AcceptedResponse>
{
    public override void Configure()
    {
        Post("/auth/forgot-password");
        AllowAnonymous();
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Request a password reset link";
            s.Description = "Sends a reset link (valid 1 hour) to an existing, non-deleted account. Always returns 202 with the same message.";
        });
    }

    public override async Task HandleAsync(EmailRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new ForgotPasswordCommand(r.Email.Trim()).ExecuteAsync(ct)).ToAccepted(HttpContext));
}

// EP-04
public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(Rules.EmailMax);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(Rules.PasswordInputMax);
    }
}

public sealed class LoginEndpoint : Endpoint<LoginRequest, TokenResponse>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Log in";
            s.Description = "Returns an access token (15 min) and a refresh token. 401 invalid-credentials for a wrong password, unknown, locked or deleted account; 403 email-not-confirmed only after a correct password.";
        });
    }

    public override async Task HandleAsync(LoginRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new LoginCommand(r.Email.Trim(), r.Password).ExecuteAsync(ct)).ToOk(HttpContext));
}

// EP-05, EP-06
public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RefreshTokenValidator : Validator<RefreshTokenRequest>
{
    public RefreshTokenValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(Rules.RefreshTokenMax);
}

public sealed class RefreshTokenEndpoint : Endpoint<RefreshTokenRequest, TokenResponse>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Options(x => x.RateLimit(RateLimitPolicies.Refresh));
        Summary(s =>
        {
            s.Summary = "Refresh the tokens";
            s.Description = "Rotates the refresh token and returns a new pair. Reusing an old refresh token revokes the whole family and returns 401.";
        });
    }

    public override async Task HandleAsync(RefreshTokenRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new RefreshTokenCommand(r.RefreshToken).ExecuteAsync(ct)).ToOk(HttpContext));
}

public sealed class LogoutEndpoint : Endpoint<RefreshTokenRequest>
{
    public override void Configure()
    {
        Post("/auth/logout");
        Policies(AuthPolicies.Authenticated);
        Summary(s =>
        {
            s.Summary = "Log out";
            s.Description = "Revokes the given refresh token if it belongs to the caller. Always 204.";
        });
    }

    public override async Task HandleAsync(RefreshTokenRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new LogoutCommand(User.UserId(), r.RefreshToken).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-07
public sealed class LogoutAllEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/logout-all");
        Policies(AuthPolicies.Authenticated);
        Summary(s =>
        {
            s.Summary = "Log out everywhere";
            s.Description = "Revokes all the caller's refresh tokens and rotates the security stamp, so existing access tokens stop working within 30 seconds.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.ResultAsync((await new LogoutAllCommand(User.UserId()).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-08
public sealed class GetCurrentUserEndpoint(IQueryHandler<GetMeQuery, Result<CurrentUserResponse>> handler) : EndpointWithoutRequest<CurrentUserResponse>
{
    public override void Configure()
    {
        Get("/auth/me");
        Policies(AuthPolicies.Authenticated);
        Summary(s =>
        {
            s.Summary = "Get the current user";
            s.Description = "Returns the caller's user ID, email, confirmation state, roles and customer ID, read from the database.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.ResultAsync((await handler.HandleAsync(new GetMeQuery(User.UserId()), ct)).ToOk(HttpContext));
}

// EP-09
public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().MaximumLength(Rules.PasswordInputMax);
        RuleFor(r => r.NewPassword).Password();
    }
}

public sealed class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest>
{
    public override void Configure()
    {
        Post("/auth/change-password");
        Policies(AuthPolicies.Authenticated);
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Change the password";
            s.Description = "Checks the current password, sets the new one, revokes all refresh tokens and rotates the security stamp.";
        });
    }

    public override async Task HandleAsync(ChangePasswordRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new ChangePasswordCommand(User.UserId(), r.CurrentPassword, r.NewPassword).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-11
public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.Token).NotEmpty().MaximumLength(Rules.TokenMax);
        RuleFor(r => r.NewPassword).Password();
    }
}

public sealed class ResetPasswordEndpoint : Endpoint<ResetPasswordRequest>
{
    public override void Configure()
    {
        Post("/auth/reset-password");
        AllowAnonymous();
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Reset the password";
            s.Description = "Sets a new password with the reset token, confirms the email, clears a failed-logins lock and revokes all tokens. Generic 400 invalid-token on any token problem.";
        });
    }

    public override async Task HandleAsync(ResetPasswordRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new ResetPasswordCommand(r.Email.Trim(), r.Token, r.NewPassword).ExecuteAsync(ct)).ToNoContent(HttpContext));
}