namespace DemoApi.UnitTests.Application;

/// <summary>Runs the unit of work inline; commit = success result (the real runner is integration-tested).</summary>
internal sealed class InlineTransactionRunner : ITransactionRunner
{
    public int Calls { get; private set; }

    public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct)
    {
        Calls++;
        return work(ct);
    }
}

internal static class Users
{
    public static UserInfo Customer(bool confirmed = true, bool deleted = false, bool locked = false) =>
        new(Guid.NewGuid(), "ada@example.com", confirmed, deleted, locked, locked ? "admin" : null, ["Customer"], Guid.NewGuid());

    public static UserInfo Admin(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "admin@example.com", true, false, false, null, ["Admin"], null);

    public static UserInfo Plain() =>
        new(Guid.NewGuid(), "user@example.com", true, false, false, null, [], null);
}

[Endpoint("EP-04")]
public sealed class LoginHandlerTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly TokenResponse _pair = new("Bearer", "a", DateTimeOffset.UnixEpoch, "r", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Unknown_email_still_checks_a_password_and_returns_invalid_credentials()
    {
        var result = await Login(null, PasswordCheck.Invalid);

        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
        await _identity.Received(1).CheckPasswordAsync(null, "pw", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PasswordCheck.Invalid)]
    [InlineData(PasswordCheck.LockedOut)]
    public async Task Wrong_password_or_locked_returns_invalid_credentials(PasswordCheck check) =>
        (await Login(Users.Customer(), check)).Error.ShouldBe(AuthErrors.InvalidCredentials);

    [Fact]
    public async Task Deleted_user_returns_invalid_credentials_even_with_correct_password() =>
        (await Login(Users.Customer(deleted: true), PasswordCheck.Valid)).Error.ShouldBe(AuthErrors.InvalidCredentials);

    [Fact]
    [Security("ST-08")]
    public async Task Unconfirmed_email_returns_403_only_after_a_correct_password()
    {
        (await Login(Users.Customer(confirmed: false), PasswordCheck.Invalid)).Error.ShouldBe(AuthErrors.InvalidCredentials);
        (await Login(Users.Customer(confirmed: false), PasswordCheck.Valid)).Error.ShouldBe(AuthErrors.EmailNotConfirmed);
    }

    [Fact]
    public async Task Valid_login_starts_a_session()
    {
        var user = Users.Customer();

        var result = await Login(user, PasswordCheck.Valid);

        result.Value.ShouldBe(_pair);
        await _tokens.Received(1).StartSessionAsync(user.Id, Arg.Any<CancellationToken>());
    }

    private Task<Result<TokenResponse>> Login(UserInfo? user, PasswordCheck check)
    {
        _identity.FindByEmailAsync("ada@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _identity.CheckPasswordAsync(Arg.Any<Guid?>(), "pw", Arg.Any<CancellationToken>()).Returns(check);
        _tokens.StartSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_pair);
        return new LoginHandler(_identity, _tokens).ExecuteAsync(new LoginCommand("ada@example.com", "pw"), TestContext.Current.CancellationToken);
    }
}

[Endpoint("EP-05")]
[Security("ST-05")]
public sealed class RefreshTokenHandlerTests
{
    [Theory]
    [InlineData(RefreshStatus.Invalid)]
    [InlineData(RefreshStatus.ReuseDetected)]
    public async Task Invalid_or_reused_token_returns_401_but_the_transaction_still_commits(RefreshStatus status)
    {
        var tokens = Substitute.For<ITokenService>();
        tokens.RotateAsync("t", Arg.Any<CancellationToken>()).Returns(new RefreshOutcome(status, null));
        var tx = new InlineTransactionRunner();

        var result = await new RefreshTokenHandler(tokens, tx).ExecuteAsync(new RefreshTokenCommand("t"), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        tx.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Rotated_token_returns_the_new_pair()
    {
        var pair = new TokenResponse("Bearer", "a", DateTimeOffset.UnixEpoch, "r2", DateTimeOffset.UnixEpoch);
        var tokens = Substitute.For<ITokenService>();
        tokens.RotateAsync("t", Arg.Any<CancellationToken>()).Returns(new RefreshOutcome(RefreshStatus.Rotated, pair));

        var result = await new RefreshTokenHandler(tokens, new InlineTransactionRunner()).ExecuteAsync(new RefreshTokenCommand("t"), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(pair);
    }
}

[Endpoint("EP-01")]
public sealed class RegisterHandlerTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly InlineTransactionRunner _tx = new();

    [Fact]
    [Story("US-001", "AC2")]
    public async Task Existing_email_returns_the_same_202_and_writes_nothing()
    {
        _identity.ValidatePasswordAsync(Arg.Any<string>(), "password", Arg.Any<string?>()).Returns(Result.Success());
        _identity.FindByEmailAsync("ada@example.com", Arg.Any<CancellationToken>()).Returns(Users.Customer());

        var result = await Handler().ExecuteAsync(Command(), TestContext.Current.CancellationToken);

        result.Value.Message.ShouldBe(Messages.Accepted);
        _tx.Calls.ShouldBe(0);
        await _email.Received(1).SendAccountExistsAsync("ada@example.com", Arg.Any<CancellationToken>());
        await _identity.DidNotReceive().CreateUserAsync(Arg.Any<NewUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blocked_password_returns_400_before_any_lookup()
    {
        _identity.ValidatePasswordAsync(Arg.Any<string>(), "password", Arg.Any<string?>()).Returns(Result.Failure(Error.Validation("password", "too common")));

        var result = await Handler().ExecuteAsync(Command(), TestContext.Current.CancellationToken);

        result.Error!.ValidationErrors!.Keys.ShouldContain("password");
        await _identity.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static RegisterCommand Command() => new("ada@example.com", "a long enough passphrase", "Ada", "Lovelace", null);

    private RegisterHandler Handler() => new(
        _identity, Substitute.For<IAppDbContext>(), _tx, _email, _clock,
        new AppSettings { RegisterResponseFloor = TimeSpan.Zero, ForgotResponseFloor = TimeSpan.Zero });
}

public sealed class SmallAuthHandlerTests
{
    private static readonly AppSettings NoFloor = new() { RegisterResponseFloor = TimeSpan.Zero, ForgotResponseFloor = TimeSpan.Zero };

    [Fact]
    [Endpoint("EP-02")]
    public async Task Confirm_email_with_bad_token_returns_invalid_token()
    {
        var identity = Substitute.For<IIdentityService>();
        identity.ConfirmEmailAsync(Arg.Any<Guid>(), "bad").Returns(false);

        var result = await new ConfirmEmailHandler(identity).ExecuteAsync(new ConfirmEmailCommand(Guid.NewGuid(), "bad"), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(Error.InvalidToken);
    }

    [Theory]
    [Endpoint("EP-10")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Forgot_password_sends_a_link_only_for_non_deleted_users(bool deleted, bool sent)
    {
        var identity = Substitute.For<IIdentityService>();
        var email = Substitute.For<IEmailSender>();
        identity.FindByEmailAsync("ada@example.com", Arg.Any<CancellationToken>()).Returns(Users.Customer(deleted: deleted));
        identity.CreatePasswordResetTokenAsync(Arg.Any<Guid>()).Returns("tok");

        var result = await new ForgotPasswordHandler(identity, email, Substitute.For<IDateTimeProvider>(), NoFloor)
            .ExecuteAsync(new ForgotPasswordCommand("ada@example.com"), TestContext.Current.CancellationToken);

        result.Value.Message.ShouldBe(Messages.Accepted);
        await email.Received(sent ? 1 : 0).SendPasswordResetAsync(Arg.Any<string>(), "tok", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Endpoint("EP-11")]
    public async Task Reset_password_for_unknown_email_returns_generic_invalid_token()
    {
        var identity = Substitute.For<IIdentityService>();
        var tokens = Substitute.For<ITokenService>();

        var result = await new ResetPasswordHandler(identity, tokens, new InlineTransactionRunner())
            .ExecuteAsync(new ResetPasswordCommand("x@example.com", "t", "a long enough passphrase"), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(Error.InvalidToken);
        await tokens.DidNotReceive().RevokeAllAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Endpoint("EP-11")]
    public async Task Reset_password_revokes_all_tokens_on_success()
    {
        var identity = Substitute.For<IIdentityService>();
        var tokens = Substitute.For<ITokenService>();
        var user = Users.Customer();
        identity.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        identity.ResetPasswordAsync(user.Id, "t", Arg.Any<string>()).Returns(Result.Success());

        var result = await new ResetPasswordHandler(identity, tokens, new InlineTransactionRunner())
            .ExecuteAsync(new ResetPasswordCommand(user.Email, "t", "a long enough passphrase"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await tokens.Received(1).RevokeAllAsync(user.Id, RevokeReasons.PasswordReset, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Endpoint("EP-08")]
    public async Task Get_me_for_deleted_user_returns_not_found()
    {
        var identity = Substitute.For<IIdentityService>();
        var user = Users.Customer(deleted: true);
        identity.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        (await new GetMeHandler(identity).HandleAsync(new GetMeQuery(user.Id), TestContext.Current.CancellationToken)).Error!.Code.ShouldBe("not-found");
    }
}

public sealed class AdminUserHandlerTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly ICurrentUser _current = Substitute.For<ICurrentUser>();
    private readonly Guid _me = Guid.NewGuid();

    public AdminUserHandlerTests() => _current.UserId.Returns(_me);

    [Fact]
    [Endpoint("EP-14")]
    public async Task Lock_self_returns_admin_safeguard()
    {
        var me = Users.Admin(_me);
        _identity.FindByIdAsync(_me, Arg.Any<CancellationToken>()).Returns(me);
        _identity.LockActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns([_me, Guid.NewGuid()]);

        var result = await new LockUserHandler(_identity, _tokens, new InlineTransactionRunner(), _current)
            .ExecuteAsync(new LockUserCommand(_me, null, null), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AdminUserErrors.AdminSafeguard);
        await _identity.DidNotReceive().LockAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset?>(), Arg.Any<string?>());
    }

    [Fact]
    [Endpoint("EP-14")]
    public async Task Lock_last_active_admin_returns_admin_safeguard()
    {
        var other = Users.Admin();
        _identity.FindByIdAsync(other.Id, Arg.Any<CancellationToken>()).Returns(other);
        _identity.LockActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns([other.Id]);

        var result = await new LockUserHandler(_identity, _tokens, new InlineTransactionRunner(), _current)
            .ExecuteAsync(new LockUserCommand(other.Id, null, null), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AdminUserErrors.AdminSafeguard);
    }

    [Fact]
    [Endpoint("EP-14")]
    public async Task Lock_customer_locks_and_revokes_sessions()
    {
        var customer = Users.Customer();
        _identity.FindByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        var until = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await new LockUserHandler(_identity, _tokens, new InlineTransactionRunner(), _current)
            .ExecuteAsync(new LockUserCommand(customer.Id, until, "fraud"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _identity.Received(1).LockAsync(customer.Id, until, "fraud");
        await _tokens.Received(1).RevokeAllAsync(customer.Id, RevokeReasons.AdminLock, Arg.Any<CancellationToken>());
        await _identity.DidNotReceive().LockActiveAdminsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Endpoint("EP-15")]
    public async Task Unlock_deleted_user_returns_user_deleted()
    {
        var deleted = Users.Customer(deleted: true);
        _identity.FindByIdAsync(deleted.Id, Arg.Any<CancellationToken>()).Returns(deleted);

        (await new UnlockUserHandler(_identity, _tokens, new InlineTransactionRunner()).ExecuteAsync(new UnlockUserCommand(deleted.Id), TestContext.Current.CancellationToken))
            .Error.ShouldBe(AdminUserErrors.UserDeleted);
    }

    [Fact]
    [Endpoint("EP-15")]
    public async Task Unlock_of_unlocked_user_is_a_no_op()
    {
        var user = Users.Customer();
        _identity.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        (await new UnlockUserHandler(_identity, _tokens, new InlineTransactionRunner()).ExecuteAsync(new UnlockUserCommand(user.Id), TestContext.Current.CancellationToken))
            .IsSuccess.ShouldBeTrue();
        await _identity.DidNotReceive().UnlockAsync(Arg.Any<Guid>());
    }

    [Fact]
    [Endpoint("EP-16")]
    public async Task Assign_admin_to_customer_returns_user_is_customer()
    {
        var customer = Users.Customer();
        _identity.FindByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);

        (await new AssignAdminRoleHandler(_identity, _tokens, new InlineTransactionRunner())
            .ExecuteAsync(new AssignAdminRoleCommand(customer.Id), TestContext.Current.CancellationToken)).Error.ShouldBe(AdminUserErrors.UserIsCustomer);
    }

    [Fact]
    [Endpoint("EP-16")]
    public async Task Assign_admin_to_plain_user_adds_role_and_revokes_sessions()
    {
        var user = Users.Plain();
        _identity.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        (await new AssignAdminRoleHandler(_identity, _tokens, new InlineTransactionRunner())
            .ExecuteAsync(new AssignAdminRoleCommand(user.Id), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        await _identity.Received(1).AddToRoleAsync(user.Id, "Admin");
        await _tokens.Received(1).RevokeAllAsync(user.Id, RevokeReasons.RoleChanged, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Endpoint("EP-17")]
    public async Task Remove_admin_from_self_returns_admin_safeguard()
    {
        _identity.FindByIdAsync(_me, Arg.Any<CancellationToken>()).Returns(Users.Admin(_me));
        _identity.LockActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns([_me, Guid.NewGuid()]);

        (await new RemoveAdminRoleHandler(_identity, _tokens, new InlineTransactionRunner(), _current)
            .ExecuteAsync(new RemoveAdminRoleCommand(_me), TestContext.Current.CancellationToken)).Error.ShouldBe(AdminUserErrors.AdminSafeguard);
        await _identity.DidNotReceive().RemoveFromRoleAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    [Endpoint("EP-17")]
    public async Task Remove_admin_from_non_admin_is_a_no_op()
    {
        var user = Users.Plain();
        _identity.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        (await new RemoveAdminRoleHandler(_identity, _tokens, new InlineTransactionRunner(), _current)
            .ExecuteAsync(new RemoveAdminRoleCommand(user.Id), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        await _identity.DidNotReceive().LockActiveAdminsAsync(Arg.Any<CancellationToken>());
    }
}