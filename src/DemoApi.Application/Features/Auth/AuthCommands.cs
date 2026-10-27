namespace DemoApi.Application.Features.Auth
{
    public static class AuthErrors
    {
        public static readonly Error InvalidCredentials =
            new("invalid-credentials", "Invalid credentials", "The email or password is wrong.", ErrorType.Unauthorized);

        public static readonly Error EmailNotConfirmed =
            new("email-not-confirmed", "Email not confirmed", "Confirm your email before you log in.", ErrorType.Forbidden);

        public static readonly Error InvalidRefreshToken =
            new("invalid-refresh-token", "Invalid refresh token", "The refresh token is invalid or has expired.", ErrorType.Unauthorized);
    }

    /// <summary>Response floor so timing does not reveal accounts (S-D6).</summary>
    internal static class ResponseFloor
    {
        public static async Task WaitAsync(DateTimeOffset started, TimeSpan floor, IDateTimeProvider clock, CancellationToken ct)
        {
            var remaining = floor - (clock.UtcNow - started);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, ct);
            }
        }
    }
}

namespace DemoApi.Application.Features.Auth.Register
{
    public sealed record RegisterCommand(string Email, string Password, string FirstName, string LastName, string? Phone)
        : ICommand<Result<AcceptedResponse>>;

    /// <summary>EP-01: user + Customer role + customer record in one transaction; always 202 (FR-002).</summary>
    public sealed class RegisterHandler(
        IIdentityService identity,
        IAppDbContext db,
        ITransactionRunner tx,
        IEmailSender email,
        IDateTimeProvider clock,
        AppSettings settings) : ICommandHandler<RegisterCommand, Result<AcceptedResponse>>
    {
        public async Task<Result<AcceptedResponse>> ExecuteAsync(RegisterCommand command, CancellationToken ct)
        {
            var started = clock.UtcNow;
            var policy = await identity.ValidatePasswordAsync(command.Password, "password", command.Email);
            if (policy.IsFailure)
            {
                return policy.Error!;
            }

            var existing = await identity.FindByEmailAsync(command.Email, ct);
            if (existing is not null)
            {
                await email.SendAccountExistsAsync(command.Email, ct);
            }
            else
            {
                var created = await tx.ExecuteAsync(
                    async token =>
                    {
                        var user = await identity.CreateUserAsync(new NewUser(command.Email, command.Password, null, null, "Customer", false), token);
                        if (user.IsFailure)
                        {
                            return Result<Guid>.Failure(user.Error!);
                        }

                        db.Customers.Add(Customer.Create(user.Value, command.FirstName, command.LastName, command.Phone));
                        return user;
                    },
                    ct);

                if (created.IsSuccess)
                {
                    var confirmToken = await identity.CreateEmailConfirmationTokenAsync(created.Value);
                    await email.SendConfirmationAsync(command.Email, created.Value, confirmToken, ct);
                }
                else if (created.Error!.Type == ErrorType.Validation)
                {
                    return created.Error;
                }
            }

            await ResponseFloor.WaitAsync(started, settings.RegisterResponseFloor, clock, ct);
            return new AcceptedResponse(Messages.Accepted);
        }
    }
}

namespace DemoApi.Application.Features.Auth.ConfirmEmail
{
    public sealed record ConfirmEmailCommand(Guid UserId, string Token) : ICommand<Result>;

    public sealed class ConfirmEmailHandler(IIdentityService identity) : ICommandHandler<ConfirmEmailCommand, Result>
    {
        public async Task<Result> ExecuteAsync(ConfirmEmailCommand command, CancellationToken ct) =>
            await identity.ConfirmEmailAsync(command.UserId, command.Token) ? Result.Success() : Error.InvalidToken;
    }
}

namespace DemoApi.Application.Features.Auth.ResendConfirmation
{
    public sealed record ResendConfirmationCommand(string Email) : ICommand<Result<AcceptedResponse>>;

    public sealed class ResendConfirmationHandler(IIdentityService identity, IEmailSender email, IDateTimeProvider clock, AppSettings settings)
        : ICommandHandler<ResendConfirmationCommand, Result<AcceptedResponse>>
    {
        public async Task<Result<AcceptedResponse>> ExecuteAsync(ResendConfirmationCommand command, CancellationToken ct)
        {
            var started = clock.UtcNow;
            var user = await identity.FindByEmailAsync(command.Email, ct);
            if (user is { EmailConfirmed: false, IsDeleted: false })
            {
                var token = await identity.CreateEmailConfirmationTokenAsync(user.Id);
                await email.SendConfirmationAsync(user.Email, user.Id, token, ct);
            }

            await ResponseFloor.WaitAsync(started, settings.ForgotResponseFloor, clock, ct);
            return new AcceptedResponse(Messages.Accepted);
        }
    }
}

namespace DemoApi.Application.Features.Auth.Login
{
    public sealed record LoginCommand(string Email, string Password) : ICommand<Result<TokenResponse>>;

    /// <summary>EP-04: password first (S-D6), then deleted/locked → 401, unconfirmed → 403.</summary>
    public sealed class LoginHandler(IIdentityService identity, ITokenService tokens) : ICommandHandler<LoginCommand, Result<TokenResponse>>
    {
        public async Task<Result<TokenResponse>> ExecuteAsync(LoginCommand command, CancellationToken ct)
        {
            var user = await identity.FindByEmailAsync(command.Email, ct);
            var check = await identity.CheckPasswordAsync(user?.Id, command.Password, ct);
            if (user is null || check != PasswordCheck.Valid || user.IsDeleted)
            {
                return AuthErrors.InvalidCredentials;
            }

            if (!user.EmailConfirmed)
            {
                return AuthErrors.EmailNotConfirmed;
            }

            return await tokens.StartSessionAsync(user.Id, ct);
        }
    }
}

namespace DemoApi.Application.Features.Auth.Refresh
{
    public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<TokenResponse>>;

    /// <summary>
    /// EP-05: rotation in one transaction. Reuse returns a success outcome so the family revocation
    /// commits before the 401 is mapped (C4, ADR-0008).
    /// </summary>
    public sealed class RefreshTokenHandler(ITokenService tokens, ITransactionRunner tx) : ICommandHandler<RefreshTokenCommand, Result<TokenResponse>>
    {
        public async Task<Result<TokenResponse>> ExecuteAsync(RefreshTokenCommand command, CancellationToken ct)
        {
            var outcome = await tx.ExecuteAsync(async token => Result.Success(await tokens.RotateAsync(command.RefreshToken, token)), ct);
            return outcome.Value is { Status: RefreshStatus.Rotated, Tokens: { } pair } ? pair : AuthErrors.InvalidRefreshToken;
        }
    }
}

namespace DemoApi.Application.Features.Auth.Logout
{
    public sealed record LogoutCommand(Guid UserId, string RefreshToken) : ICommand<Result>;

    public sealed class LogoutHandler(ITokenService tokens) : ICommandHandler<LogoutCommand, Result>
    {
        public async Task<Result> ExecuteAsync(LogoutCommand command, CancellationToken ct)
        {
            await tokens.RevokeAsync(command.UserId, command.RefreshToken, ct);
            return Result.Success();
        }
    }

    public sealed record LogoutAllCommand(Guid UserId) : ICommand<Result>;

    public sealed class LogoutAllHandler(ITokenService tokens, IIdentityService identity, ITransactionRunner tx) : ICommandHandler<LogoutAllCommand, Result>
    {
        public async Task<Result> ExecuteAsync(LogoutAllCommand command, CancellationToken ct)
        {
            await tx.ExecuteAsync(
                async token =>
                {
                    await tokens.RevokeAllAsync(command.UserId, RevokeReasons.LogoutAll, token);
                    await identity.RotateSecurityStampAsync(command.UserId);
                    return Result.Success(true);
                },
                ct);
            tokens.ForgetSession(command.UserId);
            return Result.Success();
        }
    }
}

namespace DemoApi.Application.Features.Auth.GetMe
{
    public sealed record CurrentUserResponse(Guid UserId, string Email, bool EmailConfirmed, IReadOnlyList<string> Roles, Guid? CustomerId);

    public sealed record GetMeQuery(Guid UserId);

    public sealed class GetMeHandler(IIdentityService identity) : SharedKernel.Queries.IQueryHandler<GetMeQuery, Result<CurrentUserResponse>>
    {
        public async Task<Result<CurrentUserResponse>> HandleAsync(GetMeQuery query, CancellationToken ct)
        {
            var user = await identity.FindByIdAsync(query.UserId, ct);
            return user is null || user.IsDeleted
                ? Error.NotFound()
                : new CurrentUserResponse(user.Id, user.Email, user.EmailConfirmed, user.Roles, user.CustomerId);
        }
    }
}

namespace DemoApi.Application.Features.Auth.ChangePassword
{
    public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand<Result>;

    internal sealed record ChangeOutcome(Error? Rejection);

    /// <summary>EP-09: revokes all refresh tokens and rotates the stamp (Identity does it on change).</summary>
    public sealed class ChangePasswordHandler(IIdentityService identity, ITokenService tokens, ITransactionRunner tx) : ICommandHandler<ChangePasswordCommand, Result>
    {
        public async Task<Result> ExecuteAsync(ChangePasswordCommand command, CancellationToken ct)
        {
            // A wrong current password must count toward lockout (FR-008). Returning a failed Result here would roll
            // back the access-failed counter, so the rejection travels as a successful outcome and is mapped after commit.
            var result = await tx.ExecuteAsync<ChangeOutcome>(
                async token =>
                {
                    var changed = await identity.ChangePasswordAsync(command.UserId, command.CurrentPassword, command.NewPassword);
                    if (changed.IsFailure)
                    {
                        return new ChangeOutcome(changed.Error);
                    }

                    await tokens.RevokeAllAsync(command.UserId, RevokeReasons.PasswordChanged, token);
                    return new ChangeOutcome(null);
                },
                ct);
            if (result.IsFailure)
            {
                return result.Error!;
            }

            if (result.Value.Rejection is not null)
            {
                return result.Value.Rejection;
            }

            tokens.ForgetSession(command.UserId);
            return Result.Success();
        }
    }
}

namespace DemoApi.Application.Features.Auth.ForgotPassword
{
    public sealed record ForgotPasswordCommand(string Email) : ICommand<Result<AcceptedResponse>>;

    public sealed class ForgotPasswordHandler(IIdentityService identity, IEmailSender email, IDateTimeProvider clock, AppSettings settings)
        : ICommandHandler<ForgotPasswordCommand, Result<AcceptedResponse>>
    {
        public async Task<Result<AcceptedResponse>> ExecuteAsync(ForgotPasswordCommand command, CancellationToken ct)
        {
            var started = clock.UtcNow;
            var user = await identity.FindByEmailAsync(command.Email, ct);
            if (user is { IsDeleted: false })
            {
                var token = await identity.CreatePasswordResetTokenAsync(user.Id);
                await email.SendPasswordResetAsync(user.Email, token, ct);
            }

            await ResponseFloor.WaitAsync(started, settings.ForgotResponseFloor, clock, ct);
            return new AcceptedResponse(Messages.Accepted);
        }
    }
}

namespace DemoApi.Application.Features.Auth.ResetPassword
{
    public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand<Result>;

    public sealed class ResetPasswordHandler(IIdentityService identity, ITokenService tokens, ITransactionRunner tx) : ICommandHandler<ResetPasswordCommand, Result>
    {
        public async Task<Result> ExecuteAsync(ResetPasswordCommand command, CancellationToken ct)
        {
            var user = await identity.FindByEmailAsync(command.Email, ct);
            if (user is null || user.IsDeleted)
            {
                return Error.InvalidToken;
            }

            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var reset = await identity.ResetPasswordAsync(user.Id, command.Token, command.NewPassword);
                    if (reset.IsFailure)
                    {
                        return Result<bool>.Failure(reset.Error!);
                    }

                    await tokens.RevokeAllAsync(user.Id, RevokeReasons.PasswordReset, token);
                    return true;
                },
                ct);
            tokens.ForgetSession(user.Id);
            return result.IsSuccess ? Result.Success() : result.Error!;
        }
    }
}