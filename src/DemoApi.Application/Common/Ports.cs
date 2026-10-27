namespace DemoApi.Application.Common;

/// <summary>The EF Core unit of work seen by handlers. Npgsql details stay in Infrastructure.</summary>
public interface IAppDbContext
{
    DbSet<Product> Products { get; }

    DbSet<Customer> Customers { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<OrderStatusHistory> OrderStatusHistory { get; }

    /// <summary>Identity users as a read model. Ignores the soft-delete filter (C10).</summary>
    IQueryable<UserAccountView> UserAccounts { get; }

    /// <summary>User → role name pairs.</summary>
    IQueryable<UserRoleView> UserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks product rows with <c>SELECT … FOR UPDATE</c> in ID order (P3, ADR-0007). Soft-deleted rows are returned
    /// only when <paramref name="includeDeleted"/> is set, so stock can be given back to a product deleted after the order.
    /// </summary>
    Task<List<Product>> LockProductsAsync(IReadOnlyCollection<Guid> productIds, bool includeDeleted, CancellationToken ct);

    /// <summary>Locks the customer's order rows so the Pending-order cap cannot race.</summary>
    Task LockCustomerAsync(Guid customerId, CancellationToken ct);

    /// <summary>Constraint name when the exception is a unique violation, else null.</summary>
    string? UniqueViolation(Exception exception);
}

public sealed class UserAccountView
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string NormalizedEmail { get; init; } = string.Empty;

    public bool EmailConfirmed { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public DateTimeOffset? LockoutEnd { get; init; }

    public string? LockType { get; init; }

    public int AccessFailedCount { get; init; }

    public bool IsDeleted { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class UserRoleView
{
    public Guid UserId { get; init; }

    public string RoleName { get; init; } = string.Empty;
}

public sealed record UserInfo(
    Guid Id,
    string Email,
    bool EmailConfirmed,
    bool IsDeleted,
    bool IsLockedOut,
    string? LockType,
    IReadOnlyList<string> Roles,
    Guid? CustomerId);

public sealed record NewUser(string Email, string? Password, string? FirstName, string? LastName, string Role, bool EmailConfirmed);

public enum PasswordCheck
{
    Invalid,
    Valid,
    LockedOut,
}

/// <summary>ASP.NET Core Identity behind a port, so Application has no Identity or ASP.NET Core references.</summary>
public interface IIdentityService
{
    Task<UserInfo?> FindByEmailAsync(string email, CancellationToken ct);

    Task<UserInfo?> FindByIdAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Policy check only (length, blocklist, not containing the email). Returns field errors for <paramref name="field"/>.
    /// Pass the email so the result is the same whether or not the account exists (FR-002).
    /// </summary>
    Task<Result> ValidatePasswordAsync(string password, string field, string? email = null);

    Task<Result<Guid>> CreateUserAsync(NewUser user, CancellationToken ct);

    /// <summary>
    /// Verifies the password first (equal work for unknown users via a dummy hash, S-D6). A wrong password
    /// counts toward the lockout. Does not check confirmed/deleted.
    /// </summary>
    Task<PasswordCheck> CheckPasswordAsync(Guid? userId, string password, CancellationToken ct);

    Task<string> CreateEmailConfirmationTokenAsync(Guid userId);

    Task<bool> ConfirmEmailAsync(Guid userId, string token);

    Task<string> CreatePasswordResetTokenAsync(Guid userId);

    /// <summary>Sets the password, confirms the email, clears a failed-logins lock (never an admin lock, D1).</summary>
    Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword);

    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    Task LockAsync(Guid userId, DateTimeOffset? until, string? reason);

    Task UnlockAsync(Guid userId);

    Task AddToRoleAsync(Guid userId, string role);

    Task RemoveFromRoleAsync(Guid userId, string role);

    Task RotateSecurityStampAsync(Guid userId);

    /// <summary>Anonymize, soft delete and lock the user (FR-064).</summary>
    Task EraseAsync(Guid userId);

    /// <summary>Locks active Admin rows (FOR UPDATE, ID order) and returns their IDs (last-Admin rule).</summary>
    Task<IReadOnlyList<Guid>> LockActiveAdminsAsync(CancellationToken ct);
}

public sealed record TokenResponse(
    string TokenType,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public enum RefreshStatus
{
    Rotated,
    Invalid,
    ReuseDetected,
}

public sealed record RefreshOutcome(RefreshStatus Status, TokenResponse? Tokens);

/// <summary>JWT issue and refresh-token store (ADR-0008).</summary>
public interface ITokenService
{
    Task<TokenResponse> StartSessionAsync(Guid userId, CancellationToken ct);

    /// <summary>Must run inside a transaction; reuse revocation is committed by the caller (C4).</summary>
    Task<RefreshOutcome> RotateAsync(string refreshToken, CancellationToken ct);

    Task RevokeAsync(Guid userId, string refreshToken, CancellationToken ct);

    /// <summary>Calls the <c>revoke_refresh_tokens</c> procedure (P9). Returns the number revoked.</summary>
    Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken ct);

    /// <summary>Drops the in-process security-stamp cache entry for this user.</summary>
    void ForgetSession(Guid userId);
}

public static class RevokeReasons
{
    public const string Logout = "logout";
    public const string LogoutAll = "logout-all";
    public const string PasswordChanged = "password-changed";
    public const string PasswordReset = "password-reset";
    public const string AdminLock = "admin-lock";
    public const string RoleChanged = "role-changed";
    public const string AccountDeleted = "account-deleted";
    public const string ReuseDetected = "reuse-detected";
}

/// <summary>Fake sender in this phase: logs links, never passwords (P-032).</summary>
public interface IEmailSender
{
    Task SendConfirmationAsync(string email, Guid userId, string token, CancellationToken ct);

    Task SendPasswordResetAsync(string email, string token, CancellationToken ct);

    Task SendAccountExistsAsync(string email, CancellationToken ct);
}

/// <summary>Non-secret settings from configuration.</summary>
public sealed record AppSettings
{
    public string CurrencyCode { get; init; } = "USD";

    public int MaxPendingOrders { get; init; } = 5;

    public int LowStockThreshold { get; init; } = 10;

    public TimeSpan RegisterResponseFloor { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan ForgotResponseFloor { get; init; } = TimeSpan.FromMilliseconds(250);
}

public static class Messages
{
    public const string Accepted = "If the details are valid, we have sent an email with the next steps.";
}

public sealed record AcceptedResponse(string Message);