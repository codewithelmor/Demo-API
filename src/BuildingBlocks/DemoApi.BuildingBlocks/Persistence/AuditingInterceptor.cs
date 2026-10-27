namespace DemoApi.BuildingBlocks.Persistence;

/// <summary>
/// One interceptor for the write rules (ADR-0006): fills audit columns, turns Remove into a soft delete,
/// and adds <see cref="AuditLog"/> rows to the same SaveChanges (same transaction). Personal data is
/// written as "changed" (P1).
/// </summary>
public sealed class AuditingInterceptor(IDateTimeProvider clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    private const string Changed = "changed";

    private static readonly HashSet<string> AlwaysIgnored = new(StringComparer.Ordinal)
    {
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy),
        nameof(IAuditable.LastModifiedAt), nameof(IAuditable.LastModifiedBy),
        nameof(IDeletable.DeletedAt), nameof(IDeletable.DeletedBy),
        "Version",
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var now = clock.UtcNow;
        var userId = currentUser.UserId;
        var auditUser = currentUser.AuditName;
        var traceId = Activity.Current?.TraceId.ToString();
        var rows = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged || entry.Entity is AuditLog)
            {
                continue;
            }

            var action = ApplyRules(entry, now, userId);
            if (action is null || entry.Metadata.FindAnnotation(AuditAnnotations.NotAudited) is not null)
            {
                continue;
            }

            var row = BuildRow(entry, action, now, auditUser, traceId);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        if (rows.Count > 0)
        {
            context.Set<AuditLog>().AddRange(rows);
        }
    }

    private static string? ApplyRules(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        if (entry.Entity is IdentityUserRole<Guid>)
        {
            return entry.State switch
            {
                EntityState.Added => AuditActions.RoleAssigned,
                EntityState.Deleted => AuditActions.RoleRemoved,
                _ => null,
            };
        }

        string? action = entry.State switch
        {
            EntityState.Added => AuditActions.Insert,
            EntityState.Modified => AuditActions.Update,
            EntityState.Deleted => AuditActions.SoftDelete,
            _ => null,
        };

        if (entry.Entity is IDeletable)
        {
            var isDeleted = entry.Property(nameof(IDeletable.IsDeleted));
            if (entry.State == EntityState.Deleted)
            {
                // Never delete rows: turn Remove into an update of the soft-delete columns.
                entry.State = EntityState.Modified;
                isDeleted.CurrentValue = true;
            }

            if (entry.State == EntityState.Modified && isDeleted.IsModified)
            {
                var deleted = (bool)isDeleted.CurrentValue!;
                if (deleted && !(bool)isDeleted.OriginalValue!)
                {
                    entry.Property(nameof(IDeletable.DeletedAt)).CurrentValue = now;
                    entry.Property(nameof(IDeletable.DeletedBy)).CurrentValue = userId;
                    action = AuditActions.SoftDelete;
                }
                else if (!deleted && (bool)isDeleted.OriginalValue!)
                {
                    action = AuditActions.Restore;
                }
            }
        }

        if (entry.Entity is IAuditable)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.LastModifiedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.LastModifiedBy)).CurrentValue = userId;
            }
        }

        return action;
    }

    private static AuditLog? BuildRow(EntityEntry entry, string action, DateTimeOffset now, string auditUser, string? traceId)
    {
        var changes = new Dictionary<string, object>(StringComparer.Ordinal);
        string entityType;
        string entityId;

        if (entry.Entity is IdentityUserRole<Guid> userRole)
        {
            entityType = "AppUser";
            entityId = userRole.UserId.ToString();
            changes["roleId"] = action == AuditActions.RoleAssigned
                ? new { old = (string?)null, @new = (string?)userRole.RoleId.ToString() }
                : new { old = (string?)userRole.RoleId.ToString(), @new = (string?)null };
        }
        else
        {
            entityType = entry.Metadata.FindAnnotation(AuditAnnotations.EntityName)?.Value as string ?? entry.Metadata.ClrType.Name;
            entityId = string.Join(':', entry.Metadata.FindPrimaryKey()!.Properties.Select(p => Format(entry.Property(p.Name).CurrentValue)));
            foreach (var property in entry.Properties)
            {
                var meta = property.Metadata;
                if (meta.IsPrimaryKey() || meta.IsShadowProperty() || AlwaysIgnored.Contains(meta.Name)
                    || meta.FindAnnotation(AuditAnnotations.Ignore) is not null)
                {
                    continue;
                }

                if (action == AuditActions.Update && !property.IsModified)
                {
                    continue;
                }

                if (action is AuditActions.SoftDelete or AuditActions.Restore && meta.Name != nameof(IDeletable.IsDeleted) && !property.IsModified)
                {
                    continue;
                }

                var oldValue = action == AuditActions.Insert ? null : Format(property.OriginalValue);
                var newValue = Format(property.CurrentValue);
                if (action != AuditActions.Insert && oldValue == newValue)
                {
                    continue;
                }

                changes[ToCamel(meta.Name)] = IsPersonal(meta)
                    ? Changed
                    : new { old = oldValue, @new = newValue };
            }

            if (action == AuditActions.Update && changes.Count == 0)
            {
                return null;
            }
        }

        return new AuditLog
        {
            OccurredAt = now,
            UserId = auditUser,
            TraceId = traceId,
            EntityType = entityType,
            TableName = entry.Metadata.GetTableName() ?? entityType,
            EntityId = entityId,
            Action = action,
            Changes = JsonSerializer.Serialize(changes),
        };
    }

    private static bool IsPersonal(Microsoft.EntityFrameworkCore.Metadata.IProperty meta) =>
        meta.FindAnnotation(AuditAnnotations.PersonalData) is not null
        || meta.PropertyInfo?.GetCustomAttributes(typeof(SharedKernel.Domain.PersonalDataAttribute), true).Length > 0;

    private static string ToCamel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTimeOffset d => d.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}