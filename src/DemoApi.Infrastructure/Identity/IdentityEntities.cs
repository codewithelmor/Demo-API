namespace DemoApi.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>, IAuditable, IDeletable
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary>Set for Admins only (D6); Customers keep names on the customer record.</summary>
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    /// <summary><c>failed-logins</c> or <c>admin</c> (D1).</summary>
    public string? LockType { get; set; }

    public string? LockReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? LastModifiedAt { get; set; }

    public Guid? LastModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }
}

public sealed class AppRole : IdentityRole<Guid>
{
    public static readonly Guid AdminId = new("01960000-0000-7000-8000-000000000001");
    public static readonly Guid CustomerId = new("01960000-0000-7000-8000-000000000002");

    public AppRole()
    {
    }

    public AppRole(Guid id, string name)
    {
        Id = id;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        ConcurrencyStamp = id.ToString();
    }
}

/// <summary>Refresh token row (ADR-0008). Only the SHA-256 hash is stored. Not audited.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public Guid FamilyId { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset FamilyExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public Guid? ReplacedById { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokeReason { get; set; }
}