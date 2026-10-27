


using AuthPolicies = DemoApi.BuildingBlocks.Security.Policies;

namespace DemoApi.Api.Features.Customers;

// EP-29
public sealed class CreateCustomerRequest
{
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }
}

public sealed class CreateCustomerValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.FirstName).Name();
        RuleFor(r => r.LastName).Name();
        RuleFor(r => r.Phone).Phone();
    }
}

public sealed class CreateCustomerEndpoint : Endpoint<CreateCustomerRequest, CustomerResponse>
{
    public override void Configure()
    {
        Post("/customers");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Create a customer";
            s.Description = "Creates a user with the Customer role (no password) and a customer record in one transaction; a reset link is sent so the customer sets a password. 409 duplicate-email.";
        });
    }

    public override async Task HandleAsync(CreateCustomerRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new CreateCustomerCommand(r.Email.Trim(), r.FirstName, r.LastName, r.Phone).ExecuteAsync(ct))
            .ToCreated(HttpContext, c => $"/api/v1/customers/{c.Id}"));
}

// EP-30
public sealed class ListCustomersRequest : PagedRequest
{
    public string? Search { get; set; }

    public string? CreatedFrom { get; set; }

    public string? CreatedTo { get; set; }

    public bool? IncludeDeleted { get; set; }
}

public sealed class ListCustomersValidator : PagedValidator<ListCustomersRequest>
{
    public ListCustomersValidator()
        : base(CustomerSortFields.Allowed, CustomerSortFields.Default)
    {
        RuleFor(r => r.Search).Search();
        RuleFor(r => r.CreatedFrom).IsoDate();
        RuleFor(r => r.CreatedTo).IsoDate()
            .Must((r, to) => Rules.FromBeforeTo(r.CreatedFrom, to)).WithMessage("createdTo must be after createdFrom.");
    }
}

public sealed class ListCustomersEndpoint(IQueryHandler<ListCustomersQuery, PagedResponse<CustomerListItem>> handler) : Endpoint<ListCustomersRequest, PagedResponse<CustomerListItem>>
{
    public override void Configure()
    {
        Get("/customers");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "List customers";
            s.Description = "Paged customer list. Filters: search (first name, last name, email), createdFrom (inclusive), createdTo (exclusive), includeDeleted.";
        });
    }

    public override async Task HandleAsync(ListCustomersRequest r, CancellationToken ct)
    {
        var query = new ListCustomersQuery(
            r.ToPage(), r.ToSort(CustomerSortFields.Allowed, CustomerSortFields.Default), r.Search?.Trim(),
            Rules.ParseIso(r.CreatedFrom), Rules.ParseIso(r.CreatedTo), r.IncludeDeleted ?? false);
        await Send.OkAsync(await handler.HandleAsync(query, ct), ct);
    }
}

// EP-31
public sealed class GetCustomerRequest
{
    public Guid CustomerId { get; set; }

    public bool? IncludeDeleted { get; set; }
}

public sealed class GetCustomerByIdEndpoint(IQueryHandler<GetCustomerQuery, Result<Tagged<CustomerResponse>>> handler) : Endpoint<GetCustomerRequest, CustomerResponse>
{
    public override void Configure()
    {
        Get("/customers/{customerId}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Get a customer";
            s.Description = "Returns one customer with its ETag. Deleted customers are 404 unless includeDeleted=true; they are then returned anonymized with isDeleted true.";
        });
    }

    public override async Task HandleAsync(GetCustomerRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await handler.HandleAsync(new GetCustomerQuery(r.CustomerId, r.IncludeDeleted ?? false), ct)).ToOk(HttpContext));
}

// EP-32
public sealed class UpdateCustomerRequest
{
    public Guid CustomerId { get; set; }

    [FromHeader("If-Match", false)]
    public string? IfMatch { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }
}

public sealed class UpdateCustomerValidator : Validator<UpdateCustomerRequest>
{
    public UpdateCustomerValidator()
    {
        RuleFor(r => r.FirstName).Name();
        RuleFor(r => r.LastName).Name();
        RuleFor(r => r.Phone).Phone();
        RuleFor(r => r.Email!).Email().When(r => r.Email is not null);
    }
}

public sealed class UpdateCustomerEndpoint : Endpoint<UpdateCustomerRequest, CustomerResponse>
{
    public override void Configure()
    {
        Put("/customers/{customerId}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Update a customer";
            s.Description = "Replaces first name, last name and phone (null clears). Requires If-Match: 428 when missing, 412 when stale. An absent or unchanged email is ignored; a different one returns 409 email-change-not-allowed.";
        });
    }

    public override async Task HandleAsync(UpdateCustomerRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new UpdateCustomerCommand(r.CustomerId, r.IfMatch, r.FirstName, r.LastName, r.Phone, r.Email).ExecuteAsync(ct))
            .ToOk(HttpContext));
}

// EP-33
public sealed class CustomerRouteRequest
{
    public Guid CustomerId { get; set; }
}

public sealed class DeleteCustomerEndpoint : Endpoint<CustomerRouteRequest>
{
    public override void Configure()
    {
        Delete("/customers/{customerId}");
        Policies(AuthPolicies.AdminOnly);
        Summary(s =>
        {
            s.Summary = "Delete a customer";
            s.Description = "Anonymizes and soft deletes the customer and its user, and revokes their tokens, in one transaction. 409 customer-has-open-orders when a Pending, Confirmed or Shipped order exists.";
        });
    }

    public override async Task HandleAsync(CustomerRouteRequest r, CancellationToken ct) =>
        await Send.ResultAsync((await new DeleteCustomerCommand(r.CustomerId).ExecuteAsync(ct)).ToNoContent(HttpContext));
}

// EP-35
public sealed class GetMyProfileEndpoint(IQueryHandler<GetMyProfileQuery, Result<Tagged<MyProfileResponse>>> handler) : EndpointWithoutRequest<MyProfileResponse>
{
    public override void Configure()
    {
        Get("/customers/me");
        Policies(AuthPolicies.CustomerOnly);
        Summary(s =>
        {
            s.Summary = "Get my profile";
            s.Description = "Returns the caller's customer profile with its ETag. The customer ID comes from the token only; 404 when the record no longer exists.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var customerId = User.CustomerId();
        await Send.ResultAsync(customerId == Guid.Empty
            ? Error.NotFound().ToProblem(HttpContext)
            : (await handler.HandleAsync(new GetMyProfileQuery(customerId), ct)).ToOk(HttpContext));
    }
}

// EP-36
public sealed class UpdateMyProfileRequest
{
    [FromHeader("If-Match", false)]
    public string? IfMatch { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }
}

public sealed class UpdateMyProfileValidator : Validator<UpdateMyProfileRequest>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(r => r.FirstName).Name();
        RuleFor(r => r.LastName).Name();
        RuleFor(r => r.Phone).Phone();
    }
}

public sealed class UpdateMyProfileEndpoint : Endpoint<UpdateMyProfileRequest, MyProfileResponse>
{
    public override void Configure()
    {
        Put("/customers/me");
        Policies(AuthPolicies.CustomerOnly);
        Summary(s =>
        {
            s.Summary = "Update my profile";
            s.Description = "Replaces the caller's first name, last name and phone. Requires If-Match: 428 when missing, 412 when stale. Email cannot be changed here.";
        });
    }

    public override async Task HandleAsync(UpdateMyProfileRequest r, CancellationToken ct)
    {
        var customerId = User.CustomerId();
        await Send.ResultAsync(customerId == Guid.Empty
            ? Error.NotFound().ToProblem(HttpContext)
            : (await new UpdateMyProfileCommand(customerId, r.IfMatch, r.FirstName, r.LastName, r.Phone).ExecuteAsync(ct)).ToOk(HttpContext));
    }
}

// EP-37
public sealed class EraseMyAccountRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
}

public sealed class EraseMyAccountValidator : Validator<EraseMyAccountRequest>
{
    public EraseMyAccountValidator() => RuleFor(r => r.CurrentPassword).NotEmpty().MaximumLength(Rules.PasswordInputMax);
}

public sealed class EraseMyAccountEndpoint : Endpoint<EraseMyAccountRequest>
{
    public override void Configure()
    {
        Post("/customers/me/erase");
        Policies(AuthPolicies.CustomerOnly);
        Options(x => x.RateLimit(RateLimitPolicies.AuthStrict));
        Summary(s =>
        {
            s.Summary = "Erase my account";
            s.Description = "Checks the current password (400 errors.currentPassword), then anonymizes and soft deletes the caller's customer and user and revokes all tokens. 409 customer-has-open-orders.";
        });
    }

    public override async Task HandleAsync(EraseMyAccountRequest r, CancellationToken ct)
    {
        var customerId = User.CustomerId();
        await Send.ResultAsync(customerId == Guid.Empty
            ? Error.NotFound().ToProblem(HttpContext)
            : (await new EraseMyAccountCommand(customerId, User.UserId(), r.CurrentPassword).ExecuteAsync(ct)).ToNoContent(HttpContext));
    }
}