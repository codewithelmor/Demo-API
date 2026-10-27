namespace DemoApi.IntegrationTests.Auth;

/// <summary>Token tests (ADR-0008, ST-04..ST-08). Never quarantine these.</summary>
public sealed class AuthTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    private const string NewSecret = "another long passphrase 42";

    private CapturingEmailSender Emails => (CapturingEmailSender)Factory.Services.GetRequiredService<DemoApi.Application.Common.IEmailSender>();

    private static Dictionary<string, object?> Login(string email, string secret) => Body.Of(("email", email), ("password", secret));

    private static Dictionary<string, object?> Refresh(string token) => Body.Of(("refreshToken", token));

    [Fact]
    [Endpoint("EP-01")]
    [Story("US-001", "AC1")]
    public async Task Register_confirm_login_and_me_work_end_to_end()
    {
        var client = Anonymous();
        var email = TestData.UniqueEmail("reg");

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", Body.Of(("email", email), ("password", TestData.Password), ("firstName", "Grace"), ("lastName", "Hopper")), Ct);
        await register.ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        register.Headers.CacheControl!.NoStore.ShouldBeTrue();

        // Correct secret, unconfirmed → 403 email-not-confirmed.
        await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(email, TestData.Password), Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "email-not-confirmed");

        var link = Emails.Last("confirm", email);
        (await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = link.UserId, token = link.Token }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = link.UserId, token = link.Token }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent, "already confirmed is 204");

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", Login("  " + email.ToUpperInvariant() + " ", TestData.Password), Ct);
        await login.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var tokens = await login.JsonAsync();
        tokens["tokenType"]!.GetValue<string>().ShouldBe("Bearer");
        tokens["accessTokenExpiresAt"]!.GetValue<string>().ShouldEndWith("Z");

        var me = await (await Factory.CreateApiClient(tokens["accessToken"]!.GetValue<string>()).GetAsync("/api/v1/auth/me", Ct)).JsonAsync();
        me["userId"]!.GetValue<string>().ShouldBe(link.UserId.ToString());
        me["emailConfirmed"]!.GetValue<bool>().ShouldBeTrue();
        me["roles"]!.AsArray().Select(r => r!.GetValue<string>()).ShouldBe(["Customer"]);
        me["customerId"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [Endpoint("EP-01")]
    [Security("ST-08")]
    public async Task Register_with_existing_email_returns_the_same_202_and_sends_account_exists()
    {
        var existing = await CustomerAsync();

        var response = await Anonymous().PostAsJsonAsync("/api/v1/auth/register", Body.Of(("email", existing.Email), ("password", TestData.Password), ("firstName", "X"), ("lastName", "Y")), Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        Emails.Last("exists", existing.Email).ShouldNotBeNull();
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM asp_net_users WHERE normalized_email = @e", new { e = existing.Email.ToUpperInvariant() })).ShouldBe(1);
    }

    /// <summary>FR-002 / ST-08: a password that breaks the email-name rule is rejected the same way for new and existing emails.</summary>
    [Fact]
    [Endpoint("EP-01")]
    [Security("ST-08")]
    public async Task Register_password_policy_result_is_the_same_for_existing_and_new_emails()
    {
        var existing = await CustomerAsync();
        var fresh = TestData.UniqueEmail("enum");

        async Task<(HttpStatusCode Status, string Field)> TryAsync(string email)
        {
            var local = email[..email.IndexOf('@', StringComparison.Ordinal)];
            var response = await Anonymous().PostAsJsonAsync("/api/v1/auth/register", Body.Of(("email", email), ("password", $"{local}{local}-padding"), ("firstName", "X"), ("lastName", "Y")), Ct);
            await response.ShouldBeValidationProblemAsync("password");
            return (response.StatusCode, "password");
        }

        (await TryAsync(existing.Email)).ShouldBe(await TryAsync(fresh));
    }

    [Fact]
    [Endpoint("EP-02")]
    public async Task Confirm_with_bad_token_returns_400_invalid_token()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId = Guid.NewGuid(), token = "bogus" }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "invalid-token");
    }

    [Fact]
    [Endpoint("EP-04")]
    [Security("ST-08")]
    public async Task Login_with_wrong_secret_or_unknown_email_returns_identical_401()
    {
        var customer = await CustomerAsync(TestData.Password);
        var client = Anonymous();

        var wrong = await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(customer.Email, "wrong passphrase here"), Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid-credentials");
        var unknown = await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(TestData.UniqueEmail(), "wrong passphrase here"), Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid-credentials");

        unknown["title"]!.GetValue<string>().ShouldBe(wrong["title"]!.GetValue<string>());
        unknown.ContainsKey("detail").ShouldBe(wrong.ContainsKey("detail"));
    }

    [Fact]
    [Endpoint("EP-04")]
    [Security("ST-07")]
    public async Task Five_failed_logins_lock_the_account_without_revoking_sessions()
    {
        var customer = await CustomerAsync(TestData.Password);
        var client = Anonymous();

        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/auth/login", Login(customer.Email, "wrong passphrase here"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await (await client.PostAsJsonAsync("/api/v1/auth/login", Login(customer.Email, TestData.Password), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        (await customer.Client.GetAsync("/api/v1/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "automatic lockout does not revoke sessions (S-D2)");
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM refresh_tokens WHERE user_id = @u AND revoked_at IS NULL", new { u = customer.UserId }))
            .ShouldBe(1, "the refresh token is not revoked by the automatic lockout (FR-015)");

        // While locked, refresh returns 401 without new tokens (ADR-0008 rotation rules).
        await (await client.PostAsJsonAsync("/api/v1/auth/refresh", Refresh(customer.Tokens.RefreshToken), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid-refresh-token");
    }

    [Fact]
    [Endpoint("EP-09")]
    [Security("ST-07")]
    public async Task Five_wrong_current_passwords_on_change_password_lock_the_account()
    {
        var customer = await CustomerAsync(TestData.Password);

        for (var i = 0; i < 5; i++)
        {
            await (await customer.Client.PostAsJsonAsync("/api/v1/auth/change-password", Body.Of(("currentPassword", "not it at all x"), ("newPassword", NewSecret)), Ct))
                .ShouldBeValidationProblemAsync("currentPassword");
        }

        await (await Anonymous().PostAsJsonAsync("/api/v1/auth/login", Login(customer.Email, TestData.Password), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Endpoint("EP-05")]
    [Security("ST-05")]
    public async Task Refresh_rotates_the_token_in_the_same_family()
    {
        var customer = await CustomerAsync();

        var response = await Anonymous().PostAsJsonAsync("/api/v1/auth/refresh", Refresh(customer.Tokens.RefreshToken), Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var next = await response.JsonAsync();
        next["refreshToken"]!.GetValue<string>().ShouldNotBe(customer.Tokens.RefreshToken);
        var rows = await Db.QueryAsync<RefreshRow>(
            Fixture,
            "SELECT family_id AS FamilyId, used_at AS UsedAt, replaced_by_id AS ReplacedById FROM refresh_tokens WHERE user_id = @u",
            new { u = customer.UserId });
        rows.Count.ShouldBe(2);
        rows.Select(r => r.FamilyId).Distinct().ShouldHaveSingleItem();
        rows.Count(r => r.UsedAt is not null && r.ReplacedById is not null).ShouldBe(1);
    }

    [Fact]
    [Endpoint("EP-05")]
    [Security("ST-05")]
    public async Task Reusing_a_rotated_token_returns_401_and_revokes_the_whole_family_in_the_database()
    {
        var customer = await CustomerAsync();
        var client = Anonymous();
        var second = await (await client.PostAsJsonAsync("/api/v1/auth/refresh", Refresh(customer.Tokens.RefreshToken), Ct)).JsonAsync();

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", Refresh(customer.Tokens.RefreshToken), Ct);

        await reuse.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid-refresh-token");
        (await Db.ScalarAsync<long>(Fixture, "SELECT count(*) FROM refresh_tokens WHERE user_id = @u AND revoked_at IS NULL AND used_at IS NULL", new { u = customer.UserId }))
            .ShouldBe(0, "the revocation commits before the 401 (ADR-0008 C4)");
        (await Db.ScalarAsync<string>(Fixture, "SELECT revoke_reason FROM refresh_tokens WHERE user_id = @u AND revoked_at IS NOT NULL LIMIT 1", new { u = customer.UserId }))
            .ShouldBe("reuse-detected");
        await (await client.PostAsJsonAsync("/api/v1/auth/refresh", Refresh(second["refreshToken"]!.GetValue<string>()), Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid-refresh-token");
    }

    [Fact]
    [Endpoint("EP-05")]
    [Security("ST-05")]
    public async Task Unknown_refresh_token_returns_the_same_401() =>
        await (await Anonymous().PostAsJsonAsync("/api/v1/auth/refresh", Refresh(new string('a', 43)), Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid-refresh-token");

    [Fact]
    [Endpoint("EP-05")]
    [Security("ST-05")]
    public async Task Refresh_after_the_30_day_family_limit_returns_401()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new ApiFactory(Fixture, clock: clock);
        var customer = await TestData.CustomerAsync(factory);
        var client = factory.CreateApiClient();
        var token = customer.Tokens.RefreshToken;

        // Keep the family alive with a refresh every 6 days (each token lives 7 days), until day 24.
        for (var day = 6; day < 30; day += 6)
        {
            clock.Advance(TimeSpan.FromDays(6));
            var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", Refresh(token), Ct);
            await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
            token = (await response.JsonAsync())["refreshToken"]!.GetValue<string>();
        }

        clock.Advance(TimeSpan.FromDays(6)); // day 30 + : family expired although the last token is 6 days old
        await (await client.PostAsJsonAsync("/api/v1/auth/refresh", Refresh(token), Ct))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "invalid-refresh-token");
    }

    [Fact]
    [Endpoint("EP-06")]
    public async Task Logout_revokes_one_token_and_ignores_another_users_token()
    {
        var customer = await CustomerAsync();
        var other = await CustomerAsync();

        (await customer.Client.PostAsJsonAsync("/api/v1/auth/logout", Refresh(other.Tokens.RefreshToken), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Anonymous().PostAsJsonAsync("/api/v1/auth/refresh", Refresh(other.Tokens.RefreshToken), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "another user's token is not touched");

        (await customer.Client.PostAsJsonAsync("/api/v1/auth/logout", Refresh(customer.Tokens.RefreshToken), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await Anonymous().PostAsJsonAsync("/api/v1/auth/refresh", Refresh(customer.Tokens.RefreshToken), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Endpoint("EP-07")]
    [Security("ST-06")]
    public async Task Logout_all_revokes_refresh_tokens_and_the_access_token_via_the_stamp()
    {
        var customer = await CustomerAsync();
        var second = await TestData.StartSessionAsync(Factory, customer.UserId);

        (await customer.Client.PostAsync("/api/v1/auth/logout-all", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await customer.Client.GetAsync("/api/v1/auth/me", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        await (await Anonymous().PostAsJsonAsync("/api/v1/auth/refresh", Refresh(second.RefreshToken), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Endpoint("EP-09")]
    [Security("ST-06")]
    public async Task Change_password_rotates_stamp_and_revokes_tokens()
    {
        var customer = await CustomerAsync(TestData.Password);

        var wrong = await customer.Client.PostAsJsonAsync("/api/v1/auth/change-password", Body.Of(("currentPassword", "not it at all x"), ("newPassword", NewSecret)), Ct);
        await wrong.ShouldBeValidationProblemAsync("currentPassword");

        (await customer.Client.PostAsJsonAsync("/api/v1/auth/change-password", Body.Of(("currentPassword", TestData.Password), ("newPassword", NewSecret)), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await customer.Client.GetAsync("/api/v1/auth/me", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        await (await Anonymous().PostAsJsonAsync("/api/v1/auth/refresh", Refresh(customer.Tokens.RefreshToken), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        (await Anonymous().PostAsJsonAsync("/api/v1/auth/login", Login(customer.Email, NewSecret), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>FR-128 / EP-09: "password changed" creates an Update row on the user, without hash or stamp values.</summary>
    [Fact]
    [Endpoint("EP-09")]
    [Audit("EP-09")]
    public async Task Change_password_writes_an_update_audit_row_without_the_hash()
    {
        var customer = await CustomerAsync(TestData.Password);
        var before = (await Db.AuditAsync(Fixture, "AppUser", customer.UserId)).Count;

        (await customer.Client.PostAsJsonAsync("/api/v1/auth/change-password", Body.Of(("currentPassword", TestData.Password), ("newPassword", NewSecret)), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var rows = await Db.AuditAsync(Fixture, "AppUser", customer.UserId);
        rows.Count.ShouldBe(before + 1, "FR-128: a password change creates an AuditLogs row");
        var audit = rows.Last();
        audit.Action.ShouldBe("Update");
        audit.UserId.ShouldBe(customer.UserId.ToString());
        audit.Changes.ShouldNotContain("AQAAAA", Case.Sensitive, "no hash in the audit log");
        audit.ChangesJson.ContainsKey("securityStamp").ShouldBeFalse();
    }

    [Fact]
    [Endpoint("EP-11")]
    [Story("US-008", "AC1")]
    public async Task Forgot_and_reset_password_set_the_secret_once()
    {
        var customer = await CustomerAsync(TestData.Password);
        var client = Anonymous();
        const string resetSecret = "reset long passphrase 77";

        (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = customer.Email }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = TestData.UniqueEmail() }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted, "same answer for unknown emails");
        var token = Emails.Last("reset", customer.Email).Token;

        (await client.PostAsJsonAsync("/api/v1/auth/reset-password", Body.Of(("email", customer.Email), ("token", token), ("newPassword", resetSecret)), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await client.PostAsJsonAsync("/api/v1/auth/reset-password", Body.Of(("email", customer.Email), ("token", token), ("newPassword", "third long passphrase 99")), Ct))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "invalid-token");
        await (await client.PostAsJsonAsync("/api/v1/auth/refresh", Refresh(customer.Tokens.RefreshToken), Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/auth/login", Login(customer.Email, resetSecret), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record RefreshRow(Guid FamilyId, DateTime? UsedAt, Guid? ReplacedById);
}