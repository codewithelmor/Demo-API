namespace DemoApi.Application.Features.AdminUsers
{
    public sealed record UserListItem(
        Guid Id, string Email, string? FirstName, string? LastName, bool EmailConfirmed, IReadOnlyList<string> Roles,
        bool IsLocked, Guid? CustomerId, DateTimeOffset CreatedAt, bool IsDeleted);

    public sealed record UserResponse(
        Guid Id, string Email, string? FirstName, string? LastName, bool EmailConfirmed, IReadOnlyList<string> Roles,
        DateTimeOffset? LockoutEnd, string? LockType, int FailedLoginCount, Guid? CustomerId, DateTimeOffset CreatedAt, bool IsDeleted);

    public sealed record RoleResponse(string Name, int UserCount);

    public static class AdminUserErrors
    {
        public static readonly Error AdminSafeguard = Error.Conflict(
            "admin-safeguard", "Admin safeguard", "You cannot lock or demote yourself or the last active Admin.");

        public static readonly Error UserIsCustomer = Error.Conflict(
            "user-is-customer", "User is a customer", "Customers cannot be Admins. Use a separate account.");

        public static readonly Error UserDeleted = Error.Conflict(
            "user-deleted", "User deleted", "The user is deleted.");
    }

    internal static class UserReads
    {
        public static async Task<UserResponse?> GetAsync(IAppDbContext db, Guid userId, DateTimeOffset now, CancellationToken ct)
        {
            var user = await db.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
            {
                return null;
            }

            var roles = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.RoleName).OrderBy(r => r).ToListAsync(ct);
            var customer = await db.Customers.IgnoreQueryFilters().Where(c => c.UserId == userId)
                .Select(c => new { c.Id, c.FirstName, c.LastName }).FirstOrDefaultAsync(ct);
            var locked = user.LockoutEnd is { } end && end > now;
            return new UserResponse(
                user.Id, user.Email, customer?.FirstName ?? user.FirstName, customer?.LastName ?? user.LastName, user.EmailConfirmed, roles,
                locked ? user.LockoutEnd : null, locked ? user.LockType : null, user.AccessFailedCount, customer?.Id, user.CreatedAt, user.IsDeleted);
        }
    }
}

namespace DemoApi.Application.Features.AdminUsers.ListUsers
{
    public sealed record ListUsersQuery(PageRequest Page, IReadOnlyList<SortField> Sort, string? Email, string? Role, bool? IsLocked, bool? EmailConfirmed, bool IncludeDeleted);

    public static class UserSortFields
    {
        public const string Default = "email";
        public static readonly string[] Allowed = ["email", "createdAt"];
    }

    public sealed class ListUsersHandler(IAppDbContext db, IDateTimeProvider clock) : IQueryHandler<ListUsersQuery, PagedResponse<UserListItem>>
    {
        public async Task<PagedResponse<UserListItem>> HandleAsync(ListUsersQuery q, CancellationToken ct)
        {
            var now = clock.UtcNow;
            var users = db.UserAccounts;
            if (!q.IncludeDeleted)
            {
                users = users.Where(u => !u.IsDeleted);
            }

            if (!string.IsNullOrWhiteSpace(q.Email))
            {
                var pattern = QueryHelpers.ContainsPattern(q.Email).ToUpperInvariant();
                users = users.Where(u => EF.Functions.Like(u.NormalizedEmail, pattern, "\\"));
            }

            if (!string.IsNullOrWhiteSpace(q.Role))
            {
                var role = q.Role;
                users = users.Where(u => db.UserRoles.Any(r => r.UserId == u.Id && r.RoleName == role));
            }

            if (q.IsLocked is { } isLocked)
            {
                users = isLocked ? users.Where(u => u.LockoutEnd != null && u.LockoutEnd > now) : users.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
            }

            if (q.EmailConfirmed is { } confirmed)
            {
                users = users.Where(u => u.EmailConfirmed == confirmed);
            }

            var sort = q.Sort.Count > 0 ? q.Sort[0] : new SortField("email", false);
            var ordered = sort.Name == "createdAt"
                ? (sort.Descending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt))
                : (sort.Descending ? users.OrderByDescending(u => u.NormalizedEmail) : users.OrderBy(u => u.NormalizedEmail));
            var page = await ordered.ThenBy(u => u.Id).ToPageAsync(q.Page, ct);

            var ids = page.Items.Select(u => u.Id).ToList();
            var roles = await db.UserRoles.Where(r => ids.Contains(r.UserId)).ToListAsync(ct);
            var customers = await db.Customers.IgnoreQueryFilters().Where(c => ids.Contains(c.UserId))
                .Select(c => new { c.UserId, c.Id, c.FirstName, c.LastName }).ToListAsync(ct);
            var items = page.Items.Select(u =>
            {
                var c = customers.FirstOrDefault(x => x.UserId == u.Id);
                return new UserListItem(
                    u.Id, u.Email, c?.FirstName ?? u.FirstName, c?.LastName ?? u.LastName, u.EmailConfirmed,
                    roles.Where(r => r.UserId == u.Id).Select(r => r.RoleName).OrderBy(r => r).ToList(),
                    u.LockoutEnd is { } end && end > now, c?.Id, u.CreatedAt, u.IsDeleted);
            }).ToList();
            return new PagedResponse<UserListItem>(items, page.Page, page.PageSize, page.TotalCount);
        }
    }
}

namespace DemoApi.Application.Features.AdminUsers.GetUser
{
    public sealed record GetUserQuery(Guid UserId);

    public sealed class GetUserHandler(IAppDbContext db, IDateTimeProvider clock) : IQueryHandler<GetUserQuery, Result<UserResponse>>
    {
        public async Task<Result<UserResponse>> HandleAsync(GetUserQuery query, CancellationToken ct) =>
            await UserReads.GetAsync(db, query.UserId, clock.UtcNow, ct) is { } user ? user : Error.NotFound();
    }

    public sealed record ListRolesQuery;

    public sealed class ListRolesHandler(IAppDbContext db) : IQueryHandler<ListRolesQuery, IReadOnlyList<RoleResponse>>
    {
        private static readonly string[] RoleNames = ["Admin", "Customer"];

        public async Task<IReadOnlyList<RoleResponse>> HandleAsync(ListRolesQuery query, CancellationToken ct)
        {
            var counts = await db.UserRoles
                .Where(r => db.UserAccounts.Any(u => u.Id == r.UserId && !u.IsDeleted))
                .GroupBy(r => r.RoleName).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            return RoleNames
                .Select(name => new RoleResponse(name, counts.FirstOrDefault(c => c.Key == name)?.Count ?? 0))
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .ToList();
        }
    }
}

namespace DemoApi.Application.Features.AdminUsers.ManageUsers
{
    public sealed record LockUserCommand(Guid UserId, DateTimeOffset? Until, string? Reason) : ICommand<Result>;

    /// <summary>EP-14: last-Admin rule under row locks; revokes sessions (admin-lock).</summary>
    public sealed class LockUserHandler(IIdentityService identity, ITokenService tokens, ITransactionRunner tx, ICurrentUser current)
        : ICommandHandler<LockUserCommand, Result>
    {
        public async Task<Result> ExecuteAsync(LockUserCommand command, CancellationToken ct)
        {
            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var user = await identity.FindByIdAsync(command.UserId, token);
                    if (user is null || user.IsDeleted)
                    {
                        return Result<bool>.Failure(Error.NotFound());
                    }

                    if (user.Roles.Contains("Admin"))
                    {
                        var admins = await identity.LockActiveAdminsAsync(token);
                        if (user.Id == current.UserId || (admins.Contains(user.Id) && admins.Count <= 1))
                        {
                            return Result<bool>.Failure(AdminUserErrors.AdminSafeguard);
                        }
                    }

                    await identity.LockAsync(user.Id, command.Until, command.Reason);
                    await tokens.RevokeAllAsync(user.Id, RevokeReasons.AdminLock, token);
                    return true;
                },
                ct);
            tokens.ForgetSession(command.UserId);
            return result.IsSuccess ? Result.Success() : result.Error!;
        }
    }

    public sealed record UnlockUserCommand(Guid UserId) : ICommand<Result>;

    public sealed class UnlockUserHandler(IIdentityService identity, ITokenService tokens, ITransactionRunner tx) : ICommandHandler<UnlockUserCommand, Result>
    {
        public async Task<Result> ExecuteAsync(UnlockUserCommand command, CancellationToken ct)
        {
            var user = await identity.FindByIdAsync(command.UserId, ct);
            if (user is null)
            {
                return Error.NotFound();
            }

            if (user.IsDeleted)
            {
                return AdminUserErrors.UserDeleted;
            }

            if (user.IsLockedOut || user.LockType is not null)
            {
                await tx.ExecuteAsync<bool>(
                    async _ =>
                    {
                        await identity.UnlockAsync(user.Id);
                        return true;
                    },
                    ct);
                tokens.ForgetSession(user.Id);
            }

            return Result.Success();
        }
    }

    public sealed record AssignAdminRoleCommand(Guid UserId) : ICommand<Result>;

    public sealed class AssignAdminRoleHandler(IIdentityService identity, ITokenService tokens, ITransactionRunner tx) : ICommandHandler<AssignAdminRoleCommand, Result>
    {
        public async Task<Result> ExecuteAsync(AssignAdminRoleCommand command, CancellationToken ct)
        {
            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var user = await identity.FindByIdAsync(command.UserId, token);
                    if (user is null || user.IsDeleted)
                    {
                        return Result<bool>.Failure(Error.NotFound());
                    }

                    if (user.CustomerId is not null || user.Roles.Contains("Customer"))
                    {
                        return Result<bool>.Failure(AdminUserErrors.UserIsCustomer);
                    }

                    if (!user.Roles.Contains("Admin"))
                    {
                        await identity.AddToRoleAsync(user.Id, "Admin");
                        await tokens.RevokeAllAsync(user.Id, RevokeReasons.RoleChanged, token);
                    }

                    return true;
                },
                ct);
            tokens.ForgetSession(command.UserId);
            return result.IsSuccess ? Result.Success() : result.Error!;
        }
    }

    public sealed record RemoveAdminRoleCommand(Guid UserId) : ICommand<Result>;

    public sealed class RemoveAdminRoleHandler(IIdentityService identity, ITokenService tokens, ITransactionRunner tx, ICurrentUser current)
        : ICommandHandler<RemoveAdminRoleCommand, Result>
    {
        public async Task<Result> ExecuteAsync(RemoveAdminRoleCommand command, CancellationToken ct)
        {
            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var user = await identity.FindByIdAsync(command.UserId, token);
                    if (user is null || user.IsDeleted)
                    {
                        return Result<bool>.Failure(Error.NotFound());
                    }

                    if (!user.Roles.Contains("Admin"))
                    {
                        return true;
                    }

                    var admins = await identity.LockActiveAdminsAsync(token);
                    if (user.Id == current.UserId || (admins.Contains(user.Id) && admins.Count <= 1))
                    {
                        return Result<bool>.Failure(AdminUserErrors.AdminSafeguard);
                    }

                    await identity.RemoveFromRoleAsync(user.Id, "Admin");
                    await tokens.RevokeAllAsync(user.Id, RevokeReasons.RoleChanged, token);
                    return true;
                },
                ct);
            tokens.ForgetSession(command.UserId);
            return result.IsSuccess ? Result.Success() : result.Error!;
        }
    }

    public sealed record CreateAdminUserCommand(string Email, string FirstName, string LastName) : ICommand<Result<UserResponse>>;

    /// <summary>EP-19: no password; the new Admin sets one through the reset link (logged by the fake sender).</summary>
    public sealed class CreateAdminUserHandler(IIdentityService identity, IAppDbContext db, ITransactionRunner tx, IEmailSender email, IDateTimeProvider clock)
        : ICommandHandler<CreateAdminUserCommand, Result<UserResponse>>
    {
        public async Task<Result<UserResponse>> ExecuteAsync(CreateAdminUserCommand command, CancellationToken ct)
        {
            var created = await tx.ExecuteAsync(
                token => identity.CreateUserAsync(new NewUser(command.Email, null, command.FirstName.Trim(), command.LastName.Trim(), "Admin", EmailConfirmed: true), token),
                ct);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            var resetToken = await identity.CreatePasswordResetTokenAsync(created.Value);
            await email.SendPasswordResetAsync(command.Email, resetToken, ct);
            return (await UserReads.GetAsync(db, created.Value, clock.UtcNow, ct))!;
        }
    }
}