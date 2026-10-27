namespace DemoApi.Infrastructure.Identity;

/// <summary>
/// JwtTokenIssuer + RefreshTokenStore (ADR-0008): HS256 access tokens with <c>typ: at+jwt</c> and a
/// <c>kid</c>; refresh tokens are 32 random bytes, stored as SHA-256, rotated, with reuse detection.
/// </summary>
internal sealed class TokenService(
    AppDbContext db,
    ISqlRunner sql,
    IOptions<JwtOptions> options,
    IDateTimeProvider clock,
    IMemoryCache sessionCache,
    ILogger<TokenService> logger) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public static string StampHash(string? securityStamp) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? string.Empty)).AsSpan(0, 16).ToArray());

    public static string SessionCacheKey(Guid userId) => $"session:{userId:N}";

    public async Task<TokenResponse> StartSessionAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var jwt = options.Value;
        var familyId = Guid.CreateVersion7();
        var (raw, row) = NewRefreshToken(userId, familyId, now, now.AddDays(jwt.RefreshFamilyDays));
        db.RefreshTokens.Add(row);
        var access = await CreateAccessTokenAsync(userId, now, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Session started for user {UserId}", userId);
        return new TokenResponse("Bearer", access.Token, access.ExpiresAt, raw, row.ExpiresAt);
    }

    public async Task<RefreshOutcome> RotateAsync(string refreshToken, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = Hash(refreshToken);
        var row = await db.RefreshTokens
            .FromSql($"SELECT * FROM public.refresh_tokens WHERE token_hash = {hash} FOR UPDATE")
            .AsTracking()
            .FirstOrDefaultAsync(ct);
        if (row is null || row.ExpiresAt <= now || row.FamilyExpiresAt <= now)
        {
            return new RefreshOutcome(RefreshStatus.Invalid, null);
        }

        if (row.UsedAt is not null || row.RevokedAt is not null)
        {
            var revoked = await CallRevokeAsync(row.UserId, row.FamilyId, RevokeReasons.ReuseDetected, now, ct);
            AppMetrics.RefreshReuse.Add(1);
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoked {Count} tokens in the family", row.UserId, revoked);
            return new RefreshOutcome(RefreshStatus.ReuseDetected, null);
        }

        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
        if (user is null || user.IsDeleted || !user.EmailConfirmed || (user.LockoutEnd is { } end && end > now))
        {
            return new RefreshOutcome(RefreshStatus.Invalid, null);
        }

        var (raw, next) = NewRefreshToken(row.UserId, row.FamilyId, now, row.FamilyExpiresAt);
        row.UsedAt = now;
        row.ReplacedById = next.Id;
        db.RefreshTokens.Add(next);
        var access = await CreateAccessTokenAsync(row.UserId, now, ct);
        return new RefreshOutcome(RefreshStatus.Rotated, new TokenResponse("Bearer", access.Token, access.ExpiresAt, raw, next.ExpiresAt));
    }

    public async Task RevokeAsync(Guid userId, string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var row = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId && t.RevokedAt == null, ct);
        if (row is null)
        {
            return;
        }

        row.RevokedAt = clock.UtcNow;
        row.RevokeReason = RevokeReasons.Logout;
        await db.SaveChangesAsync(ct);
    }

    public Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken ct)
    {
        ForgetSession(userId);
        return CallRevokeAsync(userId, null, reason, clock.UtcNow, ct);
    }

    public void ForgetSession(Guid userId) => sessionCache.Remove(SessionCacheKey(userId));

    private Task<int> CallRevokeAsync(Guid userId, Guid? familyId, string reason, DateTimeOffset at, CancellationToken ct) =>
        sql.RunAsync(
            (connection, transaction, token) => connection.QuerySingleAsync<int>(new CommandDefinition(
                "CALL public.revoke_refresh_tokens(@UserId, @FamilyId, @Reason, @At, NULL)",
                new { UserId = userId, FamilyId = familyId, Reason = reason, At = at },
                transaction,
                commandTimeout: 5,
                cancellationToken: token)),
            ct);

    private async Task<(string Token, DateTimeOffset ExpiresAt)> CreateAccessTokenAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var jwt = options.Value;
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.SecurityStamp }).FirstAsync(ct);
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == userId select r.Name!).ToListAsync(ct);
        var customerId = await db.Customers.Where(c => c.UserId == userId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AppClaims.Subject, userId.ToString()));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));
        identity.AddClaim(new Claim(AppClaims.SecurityStamp, StampHash(user.SecurityStamp)));
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(AppClaims.Role, role));
        }

        if (customerId is { } id)
        {
            identity.AddClaim(new Claim(AppClaims.CustomerId, id.ToString()));
        }

        var expires = now.AddMinutes(jwt.AccessTokenMinutes);
        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = identity,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            TokenType = "at+jwt",
            SigningCredentials = new SigningCredentials(jwt.CurrentKey(), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    private (string Raw, RefreshToken Row) NewRefreshToken(Guid userId, Guid familyId, DateTimeOffset now, DateTimeOffset familyExpiresAt)
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expires = now.AddDays(options.Value.RefreshTokenDays);
        return (raw, new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            IssuedAt = now,
            ExpiresAt = expires < familyExpiresAt ? expires : familyExpiresAt,
            FamilyExpiresAt = familyExpiresAt,
        });
    }

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}

/// <summary>S-D1: compares the <c>sst</c> claim with the current stamp; 30 s in-process cache; fails closed.</summary>
internal sealed class UserSessionValidator(AppDbContext db, IMemoryCache cache) : IUserSessionValidator
{
    public async Task<bool> IsValidAsync(Guid userId, string stampHash, CancellationToken ct)
    {
        var current = await cache.GetOrCreateAsync(TokenService.SessionCacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var user = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == userId)
                .Select(u => new { u.SecurityStamp, u.IsDeleted }).FirstOrDefaultAsync(ct);
            return user is null || user.IsDeleted ? null : TokenService.StampHash(user.SecurityStamp);
        });
        return current is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(current), Encoding.ASCII.GetBytes(stampHash));
    }
}