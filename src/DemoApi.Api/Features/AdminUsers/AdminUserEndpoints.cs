


using AuthPolicies = DemoApi.BuildingBlocks.Security.Policies;

namespace DemoApi.Api.Features.AdminUsers;

// EP-12
public sealed class ListUsersRequest : PagedRequest
{
    public string? Email { get; set; }

    public string? Role { get; set; }

    public bool? IsLocked { get; set; }

    public bool? EmailConfirmed { get; set; }

    public bool? IncludeDeleted { get; set; }
}

public sealed class ListUsersValidator : PagedValidator<ListUsersRequest>
{
    public ListUsersValidator()
        : base(UserSortFields.Allowed, UserSortFields.Default)
    {
        RuleFor(r => r.Email).Search();
        RuleFor(r => r.Role).Must(r => r is null or Roles.Admin or Roles.Customer).WithMessage("Use Admin or Customer.");
    }
}

public sealed class ListUsersEndpoint(IQueryHandler<ListUsersQuery, PagedResponse<UserListItem>> handler) : Endpoint<ListUsersRequest, PagedResponse<UserListItem>>
{
    public override void Configure()
    {
        Get("/admin/users");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "List users";
            s.Description = "Admins and customers with roles, lock state and customer ID. Filters: email, role, isLocked, emailConfirmed, includeDeleted.";
        });
    }

    public override async Task HandleAsync(ListUsersRequest r, CancellationToken ct)
    {
        var query = new ListUsersQuery(
            r.ToPage(), r.ToSort(UserSortFields.Allowed, UserSortFields.Default), r.Email, r.Role, r.IsLocked, r.EmailConfirmed, r.IncludeDeleted ?? false);
        await Send.OkAsync(await handler.HandleAsync(query, ct), ct);
    }
}

public sealed class UserRouteRequest
{
    public Guid UserId { get; set; }
}

// EP-13
public sealed class GetUserEndpoint(IQueryHandler<GetUserQuery, Result<UserResponse>> handler) : Endpoint<UserRouteRequest, UserResponse>
{
    public override void Configure()
    {
        Get("/admin/users/{userId}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Get a user";
            s.Description = "Returns one user with roles, lock details and failed login count. Deleted users are returned anonymized with isDeleted true.";
        });
    }

    public override async Task HandleAsync(UserRouteRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await handler.HandleAsync(new GetUserQuery(r.UserId), ct)).ToOk(HttpContext));
}

// EP-14
public sealed class LockUserRequest
{
    public Guid UserId { get; set; }

    public string? Until { get; set; }

    public string? Reason { get; set; }
}

public sealed class LockUserValidator : Validator<LockUserRequest>
{
    public LockUserValidator()
    {
        RuleFor(r => r.Until).IsoDate();
        RuleFor(r => r.Reason).Reason();
    }
}

public sealed class LockUserEndpoint : Endpoint<LockUserRequest>
{
    public override void Configure()
    {
        Post("/admin/users/{userId}/lock");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Lock a user";
            s.Description = "Admin lock until a date (or with no end) and revokes the user's sessions. You cannot lock yourself or the last active Admin (409 admin-safeguard).";
        });
    }

    public override async Task HandleAsync(LockUserRequest r, CancellationToken ct)
    {
        var until = Rules.ParseIso(r.Until);
        if (until is { } u && u <= Resolve<DemoApi.SharedKernel.Time.IDateTimeProvider>().UtcNow)
        {
            AddError(x => x.Until, "The date must be in the future.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        await Send.ResultAsync((await new LockUserCommand(r.UserId, until, r.Reason).ExecuteAsync(ct)).ToNoContent(HttpContext));
    }
}

// EP-15
public sealed class UnlockUserEndpoint : Endpoint<UserRouteRequest>
{
    public override void Configure()
    {
        Post("/admin/users/{userId}/unlock");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Unlock a user";
            s.Description = "Removes any lock (admin or failed-logins) and resets the failed login count. No-op if not locked.";
        });
    }

    public override async Task HandleAsync(UserRouteRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new UnlockUserCommand(r.UserId).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-16, EP-17
public sealed class UserRoleRequest
{
    public Guid UserId { get; set; }

    public string Role { get; set; } = string.Empty;
}

public sealed class UserRoleValidator : Validator<UserRoleRequest>
{
    public UserRoleValidator() =>
        RuleFor(r => r.Role).Must(r => string.Equals(r, Roles.Admin, StringComparison.OrdinalIgnoreCase)).WithMessage("Only the Admin role can be assigned or removed.");
}

public sealed class AssignUserRoleEndpoint : Endpoint<UserRoleRequest>
{
    public override void Configure()
    {
        Put("/admin/users/{userId}/roles/{role}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Assign the Admin role";
            s.Description = "Makes a non-customer user an Admin and revokes their sessions. Customers cannot become Admins (409 user-is-customer).";
        });
    }

    public override async Task HandleAsync(UserRoleRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new AssignAdminRoleCommand(r.UserId).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

public sealed class RemoveUserRoleEndpoint : Endpoint<UserRoleRequest>
{
    public override void Configure()
    {
        Delete("/admin/users/{userId}/roles/{role}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Remove the Admin role";
            s.Description = "Removes the Admin role and revokes the user's sessions. You cannot demote yourself or the last active Admin (409 admin-safeguard).";
        });
    }

    public override async Task HandleAsync(UserRoleRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new RemoveAdminRoleCommand(r.UserId).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-18
public sealed class ListRolesEndpoint(IQueryHandler<ListRolesQuery, IReadOnlyList<RoleResponse>> handler) : EndpointWithoutRequest<IReadOnlyList<RoleResponse>>
{
    public override void Configure()
    {
        Get("/admin/roles");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "List roles";
            s.Description = "The two roles with the number of non-deleted users in each, sorted by name.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(await handler.HandleAsync(new ListRolesQuery(), ct), ct);
}

// EP-19
public sealed class CreateAdminUserRequest
{
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;
}

public sealed class CreateAdminUserValidator : Validator<CreateAdminUserRequest>
{
    public CreateAdminUserValidator()
    {
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.FirstName).Name();
        RuleFor(r => r.LastName).Name();
    }
}

public sealed class CreateAdminUserEndpoint : Endpoint<CreateAdminUserRequest, UserResponse>
{
    public override void Configure()
    {
        Post("/admin/users");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Create an Admin";
            s.Description = "Creates a confirmed Admin user with no password; a reset link is sent so the new Admin sets one.";
        });
    }

    public override async Task HandleAsync(CreateAdminUserRequest r, CancellationToken ct)
    {
        var result = await new CreateAdminUserCommand(r.Email.Trim(), r.FirstName, r.LastName).ExecuteAsync(ct);
        await Send.ResultAsync(result.IsSuccess
            ? TypedResults.Created($"/api/v1/admin/users/{result.Value.Id}", result.Value)
            : result.ToOk(HttpContext));
    }
}