namespace DemoApi.Application.Features.Customers
{
    public sealed record CustomerResponse(
        Guid Id, Guid UserId, string Email, string FirstName, string LastName, string? Phone,
        bool EmailConfirmed, bool IsLocked, bool IsDeleted, DateTimeOffset CreatedAt);

    public sealed record CustomerListItem(
        Guid Id, Guid UserId, string FirstName, string LastName, string Email, string? Phone, DateTimeOffset CreatedAt, bool IsDeleted);

    public sealed record MyProfileResponse(
        Guid Id, string Email, string FirstName, string LastName, string? Phone, bool EmailConfirmed, DateTimeOffset CreatedAt);

    /// <summary>Customer + Identity user rows. The user join ignores the soft-delete filter (C10).</summary>
    internal sealed class CustomerRow
    {
        public required Customer Customer { get; init; }

        public required UserAccountView User { get; init; }
    }

    internal static class CustomerReads
    {
        public static IQueryable<CustomerRow> Rows(IAppDbContext db, bool includeDeleted)
        {
            // UserAccounts uses IgnoreQueryFilters, which EF applies to the whole composed query,
            // so the customer soft-delete filter must be explicit here (D13).
            var customers = includeDeleted ? db.Customers.IgnoreQueryFilters() : db.Customers.Where(c => !c.IsDeleted);
            return customers.Join(db.UserAccounts, c => c.UserId, u => u.Id, (c, u) => new CustomerRow { Customer = c, User = u });
        }

        /// <summary>
        /// Rows() is not tracked (UserAccounts uses AsNoTracking, which applies to the whole query), so writes
        /// load the customer again as a tracked entity. Null when it changed after the If-Match check (→ 412).
        /// </summary>
        public static async Task<Customer?> TrackedAsync(IAppDbContext db, CustomerRow row, CancellationToken ct)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == row.Customer.Id, ct);
            return customer is not null && customer.Version == row.Customer.Version ? customer : null;
        }

        public static Tagged<CustomerResponse> ToResponse(Customer c, UserAccountView u, DateTimeOffset now) => new(
            new CustomerResponse(
                c.Id, c.UserId, u.Email, c.FirstName, c.LastName, c.Phone, u.EmailConfirmed,
                u.LockoutEnd is { } end && end > now, c.IsDeleted, c.CreatedAt),
            ETag.Format(c.Version));

        public static Tagged<MyProfileResponse> ToProfile(Customer c, UserAccountView u) => new(
            new MyProfileResponse(c.Id, u.Email, c.FirstName, c.LastName, c.Phone, u.EmailConfirmed, c.CreatedAt),
            ETag.Format(c.Version));

        public static Task<bool> HasOpenOrdersAsync(IAppDbContext db, Guid customerId, CancellationToken ct) =>
            db.Orders.AnyAsync(
                o => o.CustomerId == customerId &&
                     (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped),
                ct);
    }

    /// <summary>EP-33 and EP-37: anonymize + soft delete customer and user, revoke tokens, in one transaction.</summary>
    internal static class CustomerEraser
    {
        public static async Task<Result<bool>> EraseAsync(
            IAppDbContext db, IIdentityService identity, ITokenService tokens, Customer customer, CancellationToken ct)
        {
            await db.LockCustomerAsync(customer.Id, ct);
            if (await CustomerReads.HasOpenOrdersAsync(db, customer.Id, ct))
            {
                return Result<bool>.Failure(CustomerErrors.HasOpenOrders);
            }

            customer.Anonymize();
            db.Customers.Remove(customer); // soft delete by the interceptor
            try
            {
                await identity.EraseAsync(customer.UserId); // saves the customer change too (same context)
                await tokens.RevokeAllAsync(customer.UserId, RevokeReasons.AccountDeleted, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<bool>.Failure(Error.ConcurrencyConflict);
            }

            return true;
        }
    }
}

namespace DemoApi.Application.Features.Customers.CreateCustomer
{
    public sealed record CreateCustomerCommand(string Email, string FirstName, string LastName, string? Phone)
        : ICommand<Result<Tagged<CustomerResponse>>>;

    /// <summary>EP-29: user (Customer role, no password) + customer in one transaction; reset link logged after commit.</summary>
    public sealed class CreateCustomerHandler(
        IIdentityService identity, IAppDbContext db, ITransactionRunner tx, IEmailSender email, IDateTimeProvider clock)
        : ICommandHandler<CreateCustomerCommand, Result<Tagged<CustomerResponse>>>
    {
        public async Task<Result<Tagged<CustomerResponse>>> ExecuteAsync(CreateCustomerCommand command, CancellationToken ct)
        {
            if (await identity.FindByEmailAsync(command.Email, ct) is not null)
            {
                return CustomerErrors.DuplicateEmail;
            }

            var created = await tx.ExecuteAsync(
                async token =>
                {
                    var user = await identity.CreateUserAsync(new NewUser(command.Email, null, null, null, "Customer", EmailConfirmed: false), token);
                    if (user.IsFailure)
                    {
                        return Result<Guid>.Failure(user.Error!);
                    }

                    var customer = Customer.Create(user.Value, command.FirstName, command.LastName, command.Phone);
                    db.Customers.Add(customer);
                    await db.SaveChangesAsync(token);
                    return customer.Id;
                },
                ct);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            var row = await CustomerReads.Rows(db, false).FirstAsync(r => r.Customer.Id == created.Value, ct);
            var resetToken = await identity.CreatePasswordResetTokenAsync(row.User.Id);
            await email.SendPasswordResetAsync(row.User.Email, resetToken, ct);
            return CustomerReads.ToResponse(row.Customer, row.User, clock.UtcNow);
        }
    }
}

namespace DemoApi.Application.Features.Customers.ListCustomers
{
    public sealed record ListCustomersQuery(
        PageRequest Page, IReadOnlyList<SortField> Sort, string? Search, DateTimeOffset? CreatedFrom, DateTimeOffset? CreatedTo, bool IncludeDeleted);

    public static class CustomerSortFields
    {
        public const string Default = "lastName";
        public static readonly string[] Allowed = ["lastName", "email", "createdAt"];
    }

    public sealed class ListCustomersHandler(IAppDbContext db) : IQueryHandler<ListCustomersQuery, PagedResponse<CustomerListItem>>
    {
        public async Task<PagedResponse<CustomerListItem>> HandleAsync(ListCustomersQuery q, CancellationToken ct)
        {
            var rows = CustomerReads.Rows(db, q.IncludeDeleted);
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var pattern = QueryHelpers.ContainsPattern(q.Search.ToLowerInvariant());
                rows = rows.Where(r =>
                    EF.Functions.Like(r.Customer.FirstName.ToLower(), pattern, "\\") ||
                    EF.Functions.Like(r.Customer.LastName.ToLower(), pattern, "\\") ||
                    EF.Functions.Like(r.User.NormalizedEmail.ToLower(), pattern, "\\"));
            }

            if (q.CreatedFrom is { } from)
            {
                rows = rows.Where(r => r.Customer.CreatedAt >= from);
            }

            if (q.CreatedTo is { } to)
            {
                rows = rows.Where(r => r.Customer.CreatedAt < to);
            }

            IOrderedQueryable<CustomerRow>? ordered = null;
            foreach (var s in q.Sort)
            {
                ordered = s.Name switch
                {
                    "email" => Order(ordered, rows, r => r.User.NormalizedEmail, s.Descending),
                    "createdAt" => Order(ordered, rows, r => r.Customer.CreatedAt, s.Descending),
                    _ => Order(ordered, rows, r => r.Customer.LastName, s.Descending),
                };
            }

            ordered ??= rows.OrderBy(r => r.Customer.LastName);
            return await ordered.ThenBy(r => r.Customer.Id)
                .Select(r => new CustomerListItem(
                    r.Customer.Id, r.Customer.UserId, r.Customer.FirstName, r.Customer.LastName, r.User.Email, r.Customer.Phone,
                    r.Customer.CreatedAt, r.Customer.IsDeleted))
                .ToPageAsync(q.Page, ct);
        }

        private static IOrderedQueryable<CustomerRow> Order<TKey>(
            IOrderedQueryable<CustomerRow>? ordered, IQueryable<CustomerRow> source, System.Linq.Expressions.Expression<Func<CustomerRow, TKey>> key, bool desc) =>
            ordered is null
                ? (desc ? source.OrderByDescending(key) : source.OrderBy(key))
                : (desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
    }
}

namespace DemoApi.Application.Features.Customers.GetCustomer
{
    public sealed record GetCustomerQuery(Guid CustomerId, bool IncludeDeleted);

    public sealed class GetCustomerHandler(IAppDbContext db, IDateTimeProvider clock) : IQueryHandler<GetCustomerQuery, Result<Tagged<CustomerResponse>>>
    {
        public async Task<Result<Tagged<CustomerResponse>>> HandleAsync(GetCustomerQuery query, CancellationToken ct)
        {
            var row = await CustomerReads.Rows(db, query.IncludeDeleted).AsNoTracking().FirstOrDefaultAsync(r => r.Customer.Id == query.CustomerId, ct);
            return row is null ? Error.NotFound() : CustomerReads.ToResponse(row.Customer, row.User, clock.UtcNow);
        }
    }
}

namespace DemoApi.Application.Features.Customers.UpdateCustomer
{
    public sealed record UpdateCustomerCommand(Guid CustomerId, string? IfMatch, string FirstName, string LastName, string? Phone, string? Email)
        : ICommand<Result<Tagged<CustomerResponse>>>;

    public sealed class UpdateCustomerHandler(IAppDbContext db, IDateTimeProvider clock) : ICommandHandler<UpdateCustomerCommand, Result<Tagged<CustomerResponse>>>
    {
        public async Task<Result<Tagged<CustomerResponse>>> ExecuteAsync(UpdateCustomerCommand command, CancellationToken ct)
        {
            var row = await CustomerReads.Rows(db, false).FirstOrDefaultAsync(r => r.Customer.Id == command.CustomerId, ct);
            if (row is null)
            {
                return Error.NotFound();
            }

            if (QueryHelpers.CheckIfMatch(command.IfMatch, row.Customer.Version) is { } precondition)
            {
                return precondition;
            }

            // D14: absent or equal email is ignored; a different one is refused.
            if (command.Email is { } email && !string.Equals(email.Trim(), row.User.Email, StringComparison.OrdinalIgnoreCase))
            {
                return CustomerErrors.EmailChangeNotAllowed;
            }

            if (await CustomerReads.TrackedAsync(db, row, ct) is not { } customer)
            {
                return Error.PreconditionFailed;
            }

            customer.UpdateProfile(command.FirstName, command.LastName, command.Phone);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.PreconditionFailed;
            }

            return CustomerReads.ToResponse(customer, row.User, clock.UtcNow);
        }
    }
}

namespace DemoApi.Application.Features.Customers.DeleteCustomer
{
    public sealed record DeleteCustomerCommand(Guid CustomerId) : ICommand<Result>;

    public sealed class DeleteCustomerHandler(IAppDbContext db, IIdentityService identity, ITokenService tokens, ITransactionRunner tx)
        : ICommandHandler<DeleteCustomerCommand, Result>
    {
        public async Task<Result> ExecuteAsync(DeleteCustomerCommand command, CancellationToken ct)
        {
            Guid? userId = null;
            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == command.CustomerId, token);
                    if (customer is null)
                    {
                        return Result<bool>.Failure(Error.NotFound());
                    }

                    userId = customer.UserId;
                    return await CustomerEraser.EraseAsync(db, identity, tokens, customer, token);
                },
                ct);
            if (userId is { } id)
            {
                tokens.ForgetSession(id);
            }

            return result.IsSuccess ? Result.Success() : result.Error!;
        }
    }
}

namespace DemoApi.Application.Features.Customers.Me
{
    public sealed record GetMyProfileQuery(Guid CustomerId);

    public sealed class GetMyProfileHandler(IAppDbContext db) : IQueryHandler<GetMyProfileQuery, Result<Tagged<MyProfileResponse>>>
    {
        public async Task<Result<Tagged<MyProfileResponse>>> HandleAsync(GetMyProfileQuery query, CancellationToken ct)
        {
            var row = await CustomerReads.Rows(db, false).AsNoTracking().FirstOrDefaultAsync(r => r.Customer.Id == query.CustomerId, ct);
            return row is null ? Error.NotFound() : CustomerReads.ToProfile(row.Customer, row.User);
        }
    }

    public sealed record UpdateMyProfileCommand(Guid CustomerId, string? IfMatch, string FirstName, string LastName, string? Phone)
        : ICommand<Result<Tagged<MyProfileResponse>>>;

    public sealed class UpdateMyProfileHandler(IAppDbContext db) : ICommandHandler<UpdateMyProfileCommand, Result<Tagged<MyProfileResponse>>>
    {
        public async Task<Result<Tagged<MyProfileResponse>>> ExecuteAsync(UpdateMyProfileCommand command, CancellationToken ct)
        {
            var row = await CustomerReads.Rows(db, false).FirstOrDefaultAsync(r => r.Customer.Id == command.CustomerId, ct);
            if (row is null)
            {
                return Error.NotFound();
            }

            if (QueryHelpers.CheckIfMatch(command.IfMatch, row.Customer.Version) is { } precondition)
            {
                return precondition;
            }

            if (await CustomerReads.TrackedAsync(db, row, ct) is not { } customer)
            {
                return Error.PreconditionFailed;
            }

            customer.UpdateProfile(command.FirstName, command.LastName, command.Phone);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.PreconditionFailed;
            }

            return CustomerReads.ToProfile(customer, row.User);
        }
    }

    public sealed record EraseMyAccountCommand(Guid CustomerId, Guid UserId, string CurrentPassword) : ICommand<Result>;

    /// <summary>EP-37: password (400) before open orders (409).</summary>
    public sealed class EraseMyAccountHandler(IAppDbContext db, IIdentityService identity, ITokenService tokens, ITransactionRunner tx)
        : ICommandHandler<EraseMyAccountCommand, Result>
    {
        public async Task<Result> ExecuteAsync(EraseMyAccountCommand command, CancellationToken ct)
        {
            if (!await db.Customers.AnyAsync(c => c.Id == command.CustomerId, ct))
            {
                return Error.NotFound();
            }

            if (await identity.CheckPasswordAsync(command.UserId, command.CurrentPassword, ct) != PasswordCheck.Valid)
            {
                return Error.Validation("currentPassword", "The current password is wrong.");
            }

            var result = await tx.ExecuteAsync(
                async token =>
                {
                    var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == command.CustomerId, token);
                    return customer is null
                        ? Result<bool>.Failure(Error.NotFound())
                        : await CustomerEraser.EraseAsync(db, identity, tokens, customer, token);
                },
                ct);
            tokens.ForgetSession(command.UserId);
            return result.IsSuccess ? Result.Success() : result.Error!;
        }
    }
}