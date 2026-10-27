namespace DemoApi.Infrastructure.Identity;

public static class LockTypes
{
    public const string FailedLogins = "failed-logins";
    public const string Admin = "admin";
}

/// <summary>Email confirmation tokens live 24 h; reset tokens 1 h (S-D3). Separate provider, separate options.</summary>
public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
}

public sealed class EmailConfirmationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<EmailConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<AppUser>> logger)
    : DataProtectorTokenProvider<AppUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "EmailConfirmation";
}

/// <summary>Password policy (FR-003, NIST SP 800-63B-4): 15-128 code points, local blocklist, no composition rules.</summary>
public sealed class PasswordPolicyValidator : IPasswordValidator<AppUser>
{
    public const int MaxLength = 128;

    private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword", "password1234567", "123456789012345", "qwertyuiopasdfgh", "iloveyouiloveyou",
        "letmeinletmeinletmein", "administrator123", "welcome12345678", "correcthorsebatterystaple",
    };

    public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (password is null)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        var length = password.EnumerateRunes().Count();
        if (length > MaxLength)
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "PasswordTooLong", Description = $"Use at most {MaxLength} characters." }));
        }

        if (Blocklist.Contains(password) || (user.Email is { Length: > 0 } email && password.Contains(email.Split('@')[0], StringComparison.OrdinalIgnoreCase) && email.Split('@')[0].Length >= 4))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "PasswordBlocked", Description = "This password is too common or contains your email name." }));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}

internal sealed partial class IdentityService(
    UserManager<AppUser> users,
    AppDbContext db,
    IPasswordHasher<AppUser> hasher,
    IDateTimeProvider clock,
    ILogger<IdentityService> logger) : IIdentityService
{
    // Hash of a random value made with the configured hasher (same iteration count), used for equal work when the email is unknown (S-D6).
    private readonly Lazy<string> dummyHash = new(() => hasher.HashPassword(new AppUser(), Guid.NewGuid().ToString("N")));

    public async Task<UserInfo?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        return user is null ? null : await ToInfoAsync(user, ct);
    }

    public async Task<UserInfo?> FindByIdAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? null : await ToInfoAsync(user, ct);
    }

    public async Task<Result> ValidatePasswordAsync(string password, string field, string? email = null)
    {
        var errors = new List<string>();
        var subject = new AppUser { UserName = email, Email = email };
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, subject, password);
            errors.AddRange(result.Errors.Select(e => e.Description));
        }

        return errors.Count == 0 ? Result.Success() : Error.Validation(field, string.Join(' ', errors.Distinct(StringComparer.Ordinal)));
    }

    public async Task<Result<Guid>> CreateUserAsync(NewUser newUser, CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = newUser.Email,
            Email = newUser.Email,
            EmailConfirmed = newUser.EmailConfirmed,
            FirstName = newUser.FirstName,
            LastName = newUser.LastName,
            LockoutEnabled = true,
        };
        var created = newUser.Password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, newUser.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return CustomerErrors.DuplicateEmail;
            }

            var field = created.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal)) ? "password" : "email";
            return Error.Validation(field, string.Join(' ', created.Errors.Select(e => e.Description)));
        }

        var role = await users.AddToRoleAsync(user, newUser.Role);
        if (!role.Succeeded)
        {
            // Inside the caller's transaction this rolls the user back; a user without a role must never exist.
            throw new InvalidOperationException($"Assigning role '{newUser.Role}' failed: {string.Join(' ', role.Errors.Select(e => e.Code))}.");
        }

        return user.Id;
    }

    public async Task<PasswordCheck> CheckPasswordAsync(Guid? userId, string password, CancellationToken ct)
    {
        var user = userId is null ? null : await users.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            hasher.VerifyHashedPassword(new AppUser(), dummyHash.Value, password);
            AppMetrics.LoginFailures.Add(1);
            return PasswordCheck.Invalid;
        }

        if (user.PasswordHash is null)
        {
            hasher.VerifyHashedPassword(user, dummyHash.Value, password);
            return PasswordCheck.Invalid;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            var wasLocked = await users.IsLockedOutAsync(user);
            await users.AccessFailedAsync(user);
            AppMetrics.LoginFailures.Add(1);
            LogLoginFailed(logger, user.Id);
            if (!wasLocked && await users.IsLockedOutAsync(user) && user.LockType is null)
            {
                user.LockType = LockTypes.FailedLogins;
                await users.UpdateAsync(user);
                LogLockedOut(logger, user.Id);
            }

            return PasswordCheck.Invalid;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return PasswordCheck.LockedOut;
        }

        if (user.AccessFailedCount > 0 || user.LockType == LockTypes.FailedLogins)
        {
            user.LockType = null;
            await users.ResetAccessFailedCountAsync(user);
        }

        return PasswordCheck.Valid;
    }

    public async Task<string> CreateEmailConfirmationTokenAsync(Guid userId) =>
        await users.GenerateEmailConfirmationTokenAsync(await Required(userId));

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            return true;
        }

        return (await users.ConfirmEmailAsync(user, token)).Succeeded;
    }

    public async Task<string> CreatePasswordResetTokenAsync(Guid userId) =>
        await users.GeneratePasswordResetTokenAsync(await Required(userId));

    public async Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.InvalidToken;
        }

        var policy = await ValidatePasswordAsync(newPassword, "newPassword", user.Email);
        if (policy.IsFailure)
        {
            return policy;
        }

        var reset = await users.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded)
        {
            return Error.InvalidToken;
        }

        user.EmailConfirmed = true;
        user.AccessFailedCount = 0;
        if (user.LockType == LockTypes.FailedLogins)
        {
            user.LockType = null;
            user.LockoutEnd = null;
        }

        await users.UpdateAsync(user);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await Required(userId);
        var policy = await ValidatePasswordAsync(newPassword, "newPassword");
        if (policy.IsFailure)
        {
            return policy;
        }

        var changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (changed.Succeeded)
        {
            return Result.Success();
        }

        if (changed.Errors.Any(e => e.Code == "PasswordMismatch"))
        {
            await users.AccessFailedAsync(user);
            return Error.Validation("currentPassword", "The current password is wrong.");
        }

        return Error.Validation("newPassword", string.Join(' ', changed.Errors.Select(e => e.Description)));
    }

    public async Task LockAsync(Guid userId, DateTimeOffset? until, string? reason)
    {
        var user = await Required(userId);
        user.LockoutEnabled = true;
        user.LockoutEnd = until ?? DateTimeOffset.MaxValue;
        user.LockType = LockTypes.Admin;
        user.LockReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
        LogAdminLocked(logger, user.Id);
    }

    public async Task UnlockAsync(Guid userId)
    {
        var user = await Required(userId);
        user.LockoutEnd = null;
        user.LockType = null;
        user.LockReason = null;
        user.AccessFailedCount = 0;
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
    }

    public async Task AddToRoleAsync(Guid userId, string role)
    {
        var user = await Required(userId);
        await users.AddToRoleAsync(user, role);
        await users.UpdateSecurityStampAsync(user);
        LogRoleChanged(logger, user.Id, role, "added");
    }

    public async Task RemoveFromRoleAsync(Guid userId, string role)
    {
        var user = await Required(userId);
        await users.RemoveFromRoleAsync(user, role);
        await users.UpdateSecurityStampAsync(user);
        LogRoleChanged(logger, user.Id, role, "removed");
    }

    // Security events: fixed IDs, user IDs only (no email, no token) so they are safe to ship (ST-19).
    [LoggerMessage(EventId = 9001, EventName = "LoginFailed", Level = LogLevel.Warning, Message = "LoginFailed userId={UserId}")]
    private static partial void LogLoginFailed(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 9002, EventName = "AccountLockedOut", Level = LogLevel.Warning, Message = "AccountLockedOut userId={UserId} after repeated failed logins")]
    private static partial void LogLockedOut(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 9003, EventName = "AccountLockedByAdmin", Level = LogLevel.Warning, Message = "AccountLockedByAdmin userId={UserId}")]
    private static partial void LogAdminLocked(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 9004, EventName = "RoleChanged", Level = LogLevel.Warning, Message = "RoleChanged userId={UserId} role={Role} change={Change}")]
    private static partial void LogRoleChanged(ILogger logger, Guid userId, string role, string change);

    public async Task RotateSecurityStampAsync(Guid userId) => await users.UpdateSecurityStampAsync(await Required(userId));

    public async Task EraseAsync(Guid userId)
    {
        var user = await Required(userId);
        var erased = $"erased-{user.Id:N}@erased.invalid";
        user.Email = erased;
        user.UserName = erased;
        user.NormalizedEmail = erased.ToUpperInvariant();
        user.NormalizedUserName = erased.ToUpperInvariant();
        user.FirstName = null;
        user.LastName = null;
        user.PhoneNumber = null;
        user.PasswordHash = null;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.LockType = LockTypes.Admin;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        db.Users.Remove(user); // soft delete by the interceptor (row kept, flagged deleted)
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Guid>> LockActiveAdminsAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        return await db.Database.SqlQuery<Guid>($"""
            SELECT u.id AS "Value"
            FROM public.asp_net_users u
            JOIN public.asp_net_user_roles ur ON ur.user_id = u.id
            WHERE ur.role_id = {AppRole.AdminId} AND u.is_deleted = false AND (u.lockout_end IS NULL OR u.lockout_end <= {now})
            ORDER BY u.id
            FOR UPDATE OF u
            """).ToListAsync(ct);
    }

    private async Task<AppUser> Required(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");

    private async Task<UserInfo> ToInfoAsync(AppUser user, CancellationToken ct)
    {
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == user.Id select r.Name!).ToListAsync(ct);
        var customerId = await db.Customers.IgnoreQueryFilters().Where(c => c.UserId == user.Id).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        var locked = user.LockoutEnd is { } end && end > clock.UtcNow;
        return new UserInfo(user.Id, user.Email ?? string.Empty, user.EmailConfirmed, user.IsDeleted, locked, user.LockType, roles, customerId);
    }
}