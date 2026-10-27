namespace DemoApi.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public static void ConfigureAuditColumns<T>(this EntityTypeBuilder<T> builder, string table)
        where T : class, IDeletable
    {
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.ToTable(table, t => t.HasCheckConstraint(
            $"ck_{table}_deleted_consistent",
            "(is_deleted AND deleted_at IS NOT NULL) OR (NOT is_deleted AND deleted_at IS NULL AND deleted_by IS NULL)"));
    }

    public static void HasXminVersion<T>(this EntityTypeBuilder<T> builder, System.Linq.Expressions.Expression<Func<T, uint>> version)
        where T : class =>
        builder.Property(version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ConfigureAuditColumns("products");
        builder.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_sku", "sku ~ '^[A-Z0-9-]{1,50}$'");
            t.HasCheckConstraint("ck_products_name_len", "length(name) BETWEEN 1 AND 200");
            t.HasCheckConstraint("ck_products_price", "price >= 0.01");
            t.HasCheckConstraint("ck_products_stock", "stock_quantity BETWEEN 0 AND 1000000");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength);
        builder.Property(p => p.Sku).HasMaxLength(Product.SkuMaxLength);
        builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);
        builder.Property(p => p.Price).HasPrecision(12, 2);
        builder.Property(p => p.StockQuantity).HasDefaultValue(0);
        builder.HasXminVersion(p => p.Version);
        builder.HasIndex(p => p.Sku).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_products_sku");
        builder.HasIndex(p => new { p.StockQuantity, p.Id }).HasFilter("is_deleted = false").HasDatabaseName("ix_products_stock");
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ConfigureAuditColumns("customers");
        builder.ToTable("customers", t =>
        {
            t.HasCheckConstraint("ck_customers_first_name_len", "length(first_name) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_customers_last_name_len", "length(last_name) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_customers_phone", "phone ~ '^\\+[1-9][0-9]{1,18}$'");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.FirstName).HasMaxLength(Customer.NameMaxLength);
        builder.Property(c => c.LastName).HasMaxLength(Customer.NameMaxLength);
        builder.Property(c => c.Phone).HasMaxLength(Customer.PhoneMaxLength);
        builder.HasXminVersion(c => c.Version);
        builder.HasOne<AppUser>().WithOne().HasForeignKey<Customer>(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.UserId).IsUnique().HasDatabaseName("ux_customers_user_id");
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ConfigureAuditColumns("orders");
        builder.ToTable("orders", t =>
        {
            t.HasCheckConstraint("ck_orders_status", "status IN ('Pending','Confirmed','Shipped','Completed','Cancelled')");
            t.HasCheckConstraint("ck_orders_total", "total >= 0");
            t.HasCheckConstraint("ck_orders_currency", "currency_code ~ '^[A-Z]{3}$'");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Total).HasPrecision(12, 2);
        builder.Property(o => o.CurrencyCode).HasMaxLength(3).IsFixedLength();
        builder.HasXminVersion(o => o.Version);
        builder.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.History).WithOne().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(o => o.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(o => o.ActiveItems);
        builder.HasIndex(o => new { o.CustomerId, o.CreatedAt, o.Id }).IsDescending(false, true, true)
            .HasFilter("is_deleted = false").HasDatabaseName("ix_orders_customer_created");
        builder.HasIndex(o => new { o.CreatedAt, o.Id }).IsDescending(true, true)
            .HasFilter("is_deleted = false").HasDatabaseName("ix_orders_created");
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ConfigureAuditColumns("order_items");
        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("ck_order_items_quantity", "quantity BETWEEN 1 AND 1000");
            t.HasCheckConstraint("ck_order_items_unit_price", "unit_price >= 0.01");
            t.HasCheckConstraint("ck_order_items_line_total", "line_total = quantity * unit_price");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);
        builder.Property(i => i.LineTotal).HasPrecision(12, 2);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.OrderId, i.ProductId }).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_order_items_order_product");
        builder.HasIndex(i => i.ProductId).HasFilter("is_deleted = false").HasDatabaseName("ix_order_items_product");
    }
}

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ConfigureAuditColumns("order_status_history");
        const string statuses = "('Pending','Confirmed','Shipped','Completed','Cancelled')";
        builder.ToTable("order_status_history", t =>
        {
            t.HasCheckConstraint("ck_order_status_history_from", $"from_status IS NULL OR from_status IN {statuses}");
            t.HasCheckConstraint("ck_order_status_history_to", $"to_status IN {statuses}");
            t.HasCheckConstraint("ck_order_status_history_change", "from_status IS DISTINCT FROM to_status");
        });
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Reason).HasMaxLength(500);
        builder.HasIndex(h => new { h.OrderId, h.CreatedAt, h.Id }).HasDatabaseName("ix_order_status_history_order");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public static readonly string[] Reasons =
        ["logout", "logout-all", "password-changed", "password-reset", "admin-lock", "role-changed", "account-deleted", "reuse-detected", "admin-revoked"];

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", t =>
        {
            t.HasCheckConstraint("ck_refresh_tokens_hash_len", "octet_length(token_hash) = 32");
            t.HasCheckConstraint("ck_refresh_tokens_expiry", "expires_at <= family_expires_at");
            t.HasCheckConstraint("ck_refresh_tokens_reason", $"revoke_reason IN ({string.Join(',', Reasons.Select(r => $"'{r}'"))})");
            t.HasCheckConstraint("ck_refresh_tokens_revoked", "(revoked_at IS NULL) = (revoke_reason IS NULL)");
        });
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.RevokeReason).HasMaxLength(32);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(t => t.ReplacedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(t => t.UserId, "ix_refresh_tokens_user");
        builder.HasIndex(t => t.UserId, "ix_refresh_tokens_user_active").HasFilter("revoked_at IS NULL AND used_at IS NULL");
        builder.HasIndex(t => t.FamilyId).HasFilter("revoked_at IS NULL").HasDatabaseName("ix_refresh_tokens_family");
        builder.HasAnnotation(AuditAnnotations.NotAudited, true);
    }
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ConfigureAuditColumns("asp_net_users");
        builder.ToTable("asp_net_users", t =>
        {
            t.HasCheckConstraint("ck_asp_net_users_lock_type", "lock_type IN ('failed-logins','admin')");
            t.HasCheckConstraint("ck_asp_net_users_lock_consistent", "lock_type IS NULL OR lockout_end IS NOT NULL");
        });
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.LockType).HasMaxLength(20);
        builder.Property(u => u.LockReason).HasMaxLength(500);
        builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_asp_net_users_normalized_user_name");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_asp_net_users_normalized_email");
        builder.HasAnnotation(AuditAnnotations.EntityName, "AppUser");

        foreach (var name in new[] { nameof(AppUser.Email), nameof(AppUser.NormalizedEmail), nameof(AppUser.UserName), nameof(AppUser.NormalizedUserName), nameof(AppUser.PhoneNumber), nameof(AppUser.FirstName), nameof(AppUser.LastName), nameof(AppUser.PasswordHash) })
        {
            builder.Property(name).HasAnnotation(AuditAnnotations.PersonalData, true);
        }

        foreach (var name in new[] { nameof(AppUser.SecurityStamp), nameof(AppUser.ConcurrencyStamp), nameof(AppUser.AccessFailedCount) })
        {
            builder.Property(name).HasAnnotation(AuditAnnotations.Ignore, true);
        }
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.HasData(new AppRole(AppRole.AdminId, "Admin"), new AppRole(AppRole.CustomerId, "Customer"));
        builder.HasAnnotation(AuditAnnotations.NotAudited, true);
    }
}

internal sealed class IdentityUnusedConfiguration :
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.HasAnnotation(AuditAnnotations.NotAudited, true);

    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.HasAnnotation(AuditAnnotations.NotAudited, true);

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.HasAnnotation(AuditAnnotations.NotAudited, true);

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.HasAnnotation(AuditAnnotations.NotAudited, true);
}