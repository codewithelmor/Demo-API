namespace DemoApi.Bff.Session;

/// <summary>
/// The session store (Valkey) cannot be reached. Mapped to 503; sign-in fails closed (T-54). It keeps only the type
/// name of the cause, never the exception itself: a Redis error text can hold the cache key, which is the session ID.
/// </summary>
internal sealed class SessionStoreUnavailableException(string causeType)
    : Exception("The session store is not available. Cause: " + causeType);

/// <summary>
/// Sessions in an <see cref="IDistributedCache"/> (Valkey). Each entry is serialized and encrypted with Data
/// Protection using a protector whose purpose includes the session ID, so a blob copied to another key cannot be
/// read (NFR-F30, T-44). Writes that must not race (replace, touch, delete) are serialized per session. That is
/// enough for one BFF instance (ADR-0015); more instances need a Valkey lock.
/// </summary>
internal sealed class SessionStore(IDistributedCache cache, IDataProtectionProvider protection) : IDisposable
{
    private const string Purpose = "DemoApi.Bff.Session.v1";
    private const string KeyPrefix = "bff:session:";
    private const int GateCount = 64;

    private readonly IDataProtector root = protection.CreateProtector(Purpose);
    private readonly SemaphoreSlim[] gates = [.. Enumerable.Range(0, GateCount).Select(static _ => new SemaphoreSlim(1, 1))];

    public async Task<SessionRecord> CreateAsync(
        TokenSet tokens,
        UserSummary user,
        DateTimeOffset now,
        CancellationToken ct)
    {
        SessionRecord record = new(
            SessionIds.New(),
            tokens.AccessToken,
            tokens.AccessTokenExpiresAt,
            tokens.RefreshToken,
            tokens.RefreshTokenExpiresAt,
            user,
            now,
            now,
            1);
        await WriteAsync(record, ct);
        return record;
    }

    public async Task<SessionRecord?> GetAsync(string? id, CancellationToken ct) => (await ReadAsync(id, ct)).Record;

    /// <summary>Like <see cref="GetAsync"/>, but says whether an entry existed and could not be read (and was removed).</summary>
    public async Task<(SessionRecord? Record, bool Tampered)> ReadAsync(string? id, CancellationToken ct)
    {
        if (!SessionIds.IsValid(id))
        {
            return (null, false);
        }

        byte[]? blob = await Io(() => cache.GetAsync(Key(id!), ct));
        if (blob is null)
        {
            return (null, false);
        }

        SessionRecord? record = Decode(id!, blob);
        if (record is null)
        {
            // Tampered, copied from another key, or encrypted with keys we no longer have: end it.
            await DeleteAsync(id!);
        }

        return (record, record is null);
    }

    /// <summary>Saves a refresh result only if the session still exists with the same version (NFR-F31).</summary>
    public Task<SessionRecord?> TryReplaceAsync(string id, long expectedVersion, SessionRecord updated, CancellationToken ct) =>
        WithGateAsync<SessionRecord?>(id, async () =>
        {
            SessionRecord? current = await ReadUnlockedAsync(id, ct);
            if (current is null || current.Version != expectedVersion)
            {
                return null;
            }

            // Keep activity recorded while EP-05 ran; a refresh must not rewind it.
            SessionRecord merged = updated with { LastActivityAt = current.LastActivityAt };
            await WriteAsync(merged, ct);
            return merged;
        });

    public Task TouchAsync(string id, DateTimeOffset now, CancellationToken ct) =>
        WithGateAsync(id, async () =>
        {
            SessionRecord? current = await ReadUnlockedAsync(id, ct);
            if (current is not null)
            {
                await WriteAsync(current with { LastActivityAt = now }, ct);
            }

            return true;
        });

    /// <summary>Deletes the session. Never cancelled: a sign-out must finish even if the browser goes away.</summary>
    public Task DeleteAsync(string id) =>
        SessionIds.IsValid(id)
            ? WithGateAsync(id, async () =>
            {
                await Io(() => cache.RemoveAsync(Key(id), CancellationToken.None));
                return true;
            })
            : Task.CompletedTask;

    public void Dispose()
    {
        foreach (SemaphoreSlim gate in gates)
        {
            gate.Dispose();
        }
    }

    private static string Key(string id) => KeyPrefix + id;

    private async Task<SessionRecord?> ReadUnlockedAsync(string id, CancellationToken ct)
    {
        byte[]? blob = await Io(() => cache.GetAsync(Key(id), ct));
        return blob is null ? null : Decode(id, blob);
    }

    private SessionRecord? Decode(string id, byte[] blob)
    {
        try
        {
            byte[] plain = root.CreateProtector(id).Unprotect(blob);
            SessionRecord? record = JsonSerializer.Deserialize<SessionRecord>(plain);
            return record is not null && string.Equals(record.SessionId, id, StringComparison.Ordinal) ? record : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private async Task WriteAsync(SessionRecord record, CancellationToken ct)
    {
        byte[] blob = root.CreateProtector(record.SessionId).Protect(JsonSerializer.SerializeToUtf8Bytes(record));
        DistributedCacheEntryOptions options = new() { AbsoluteExpiration = record.RefreshTokenExpiresAt };
        await Io(() => cache.SetAsync(Key(record.SessionId), blob, options, ct));
    }

    private async Task<T> WithGateAsync<T>(string id, Func<Task<T>> action)
    {
        SemaphoreSlim gate = gates[(uint)StringComparer.Ordinal.GetHashCode(id) % GateCount];
        await gate.WaitAsync(CancellationToken.None);
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<T> Io<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or SessionStoreUnavailableException))
        {
            throw new SessionStoreUnavailableException(ex.GetType().Name);
        }
    }

    private static async Task Io(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or SessionStoreUnavailableException))
        {
            throw new SessionStoreUnavailableException(ex.GetType().Name);
        }
    }
}