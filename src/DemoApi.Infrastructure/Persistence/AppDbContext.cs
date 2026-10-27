namespace DemoApi.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public IQueryable<UserAccountView> UserAccounts => Users.IgnoreQueryFilters().AsNoTracking().Select(u => new UserAccountView
    {
        Id = u.Id,
        Email = u.Email!,
        NormalizedEmail = u.NormalizedEmail!,
        EmailConfirmed = u.EmailConfirmed,
        FirstName = u.FirstName,
        LastName = u.LastName,
        LockoutEnd = u.LockoutEnd,
        LockType = u.LockType,
        AccessFailedCount = u.AccessFailedCount,
        IsDeleted = u.IsDeleted,
        CreatedAt = u.CreatedAt,
    });

    IQueryable<UserRoleView> IAppDbContext.UserRoles =>
        from ur in UserRoles.AsNoTracking()
        join r in Roles.AsNoTracking() on ur.RoleId equals r.Id
        select new UserRoleView { UserId = ur.UserId, RoleName = r.Name! };

    public Task<List<Product>> LockProductsAsync(IReadOnlyCollection<Guid> productIds, bool includeDeleted, CancellationToken ct)
    {
        var ids = productIds.ToArray();
        return Products
            // xmin is a system column, so SELECT * does not return it; EF needs it as the concurrency token.
            .FromSql($"SELECT *, xmin FROM public.products WHERE id = ANY({ids}) AND (is_deleted = false OR {includeDeleted}) ORDER BY id FOR UPDATE")
            .IgnoreQueryFilters()
            .AsTracking()
            .ToListAsync(ct);
    }

    public async Task LockCustomerAsync(Guid customerId, CancellationToken ct) =>
        await Database.ExecuteSqlAsync($"SELECT 1 FROM public.customers WHERE id = {customerId} FOR UPDATE", ct);

    public string? UniqueViolation(Exception exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrors.UniqueViolation } pg ? pg.ConstraintName ?? "unique" : null;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Identity sets explicit PascalCase names; the snake_case convention does not override them.
        builder.Entity<AppRole>().ToTable("asp_net_roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        builder.ApplyConfiguration(new AuditLogConfiguration());
        builder.ApplySoftDeleteFilters();
        builder.ApplyPersonalDataAnnotations();
    }
}