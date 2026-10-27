namespace DemoApi.SharedKernel.Domain;

/// <summary>Entities with created/modified columns, set by the auditing interceptor.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    Guid? CreatedBy { get; }

    DateTimeOffset? LastModifiedAt { get; }

    Guid? LastModifiedBy { get; }
}

/// <summary>Entities that are never deleted; Remove is turned into a soft delete.</summary>
public interface IDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedBy { get; }
}

/// <summary>
/// Marks a property as personal data. Its value is never written to the audit log
/// (the change is recorded as "changed"), and it is anonymized on erase (P1, FR-064).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class PersonalDataAttribute : Attribute
{
}