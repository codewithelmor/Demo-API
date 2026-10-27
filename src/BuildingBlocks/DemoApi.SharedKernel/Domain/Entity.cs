namespace DemoApi.SharedKernel.Domain;

/// <summary>Base type for entities with a GUID (v7) key created by the domain.</summary>
public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    protected Entity()
    {
    }

    public Guid Id { get; private init; }
}

/// <summary>
/// Business entity base: audit columns are set by the auditing interceptor,
/// soft-delete columns by the soft-delete interceptor (Remove becomes Update).
/// </summary>
public abstract class AuditableEntity : Entity, IAuditable, IDeletable
{
    protected AuditableEntity(Guid id)
        : base(id)
    {
    }

    protected AuditableEntity()
    {
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? LastModifiedAt { get; private set; }

    public Guid? LastModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>Clears the soft-delete flag. The interceptor records a Restore audit row.</summary>
    protected void ClearDeleted()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    /// <summary>Soft deletes from inside an aggregate. The interceptor fills DeletedAt/DeletedBy.</summary>
    protected void MarkDeleted() => IsDeleted = true;

    /// <summary>Forces a row update so the xmin ETag changes (ADR-0007).</summary>
    protected void Touch(DateTimeOffset now) => LastModifiedAt = now;
}