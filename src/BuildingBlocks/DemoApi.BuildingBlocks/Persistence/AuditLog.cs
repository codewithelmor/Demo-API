namespace DemoApi.BuildingBlocks.Persistence;

public static class AuditActions
{
    public const string Insert = "Insert";
    public const string Update = "Update";
    public const string SoftDelete = "SoftDelete";
    public const string Restore = "Restore";
    public const string RoleAssigned = "RoleAssigned";
    public const string RoleRemoved = "RoleRemoved";
    public const string SessionsRevoked = "SessionsRevoked";

    public static readonly IReadOnlyList<string> All =
        [Insert, Update, SoftDelete, Restore, RoleAssigned, RoleRemoved, SessionsRevoked];
}

/// <summary>Append-only audit row, written in the same transaction as the change (ADR-0006).</summary>
public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; }

    public required string UserId { get; init; }

    public string? TraceId { get; init; }

    public required string EntityType { get; init; }

    public required string TableName { get; init; }

    public required string EntityId { get; init; }

    public required string Action { get; init; }

    /// <summary>JSON: <c>{"price":{"old":"10.00","new":"12.00"},"email":"changed"}</c>.</summary>
    public string Changes { get; init; } = "{}";
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", t => t.HasCheckConstraint(
            "ck_audit_logs_action",
            $"action IN ({string.Join(',', AuditActions.All.Select(a => $"'{a}'"))})"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).HasMaxLength(36);
        builder.Property(a => a.TraceId).HasMaxLength(32);
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.TableName).HasMaxLength(128);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.Action).HasMaxLength(20);
        builder.Property(a => a.Changes).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        builder.HasIndex(a => new { a.OccurredAt, a.Id }).IsDescending(true, true).HasDatabaseName("ix_audit_logs_occurred");
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAt }).IsDescending(false, false, true).HasDatabaseName("ix_audit_logs_entity");
        builder.HasIndex(a => new { a.UserId, a.OccurredAt }).IsDescending(false, true).HasDatabaseName("ix_audit_logs_user");
        builder.HasAnnotation(AuditAnnotations.NotAudited, true);
    }
}

public static class AuditAnnotations
{
    /// <summary>Entity type annotation: no audit rows (audit_logs, refresh_tokens, roles).</summary>
    public const string NotAudited = "DemoApi:NotAudited";

    /// <summary>Property annotation: value never written (written as "changed").</summary>
    public const string PersonalData = "DemoApi:PersonalData";

    /// <summary>Property annotation: never appears in the audit log (hashes, stamps, counters).</summary>
    public const string Ignore = "DemoApi:AuditIgnore";

    /// <summary>Entity type annotation: audit rows use this entity name instead of the CLR name.</summary>
    public const string EntityName = "DemoApi:AuditEntityName";
}