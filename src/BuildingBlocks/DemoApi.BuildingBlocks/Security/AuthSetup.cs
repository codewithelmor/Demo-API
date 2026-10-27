namespace DemoApi.BuildingBlocks.Security;

/// <summary>
/// Per-request security-stamp check (S-D1). Implemented in Infrastructure; must fail closed.
/// </summary>
public interface IUserSessionValidator
{
    Task<bool> IsValidAsync(Guid userId, string stampHash, CancellationToken ct);
}

public static class AuthSetup
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Section).ValidateDataAnnotations().ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidTypes = ["at+jwt"],
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = jwt.ValidationKeys(),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaims.Subject,
                    RoleClaimType = AppClaims.Role,
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSessionAsync,
                    OnChallenge = context =>
                    {
                        // The status-code page writer adds the ProblemDetails body.
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin, Roles.Customer))
            .AddPolicy(Policies.AdminOnly, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin))
            .AddPolicy(Policies.CustomerOnly, p => p.RequireAuthenticatedUser().RequireRole(Roles.Customer)
                .RequireAssertion(c => Guid.TryParse(c.User.FindFirstValue(AppClaims.CustomerId), out _)));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirstValue(AppClaims.Subject), out var userId)
            || principal.FindFirstValue(AppClaims.SecurityStamp) is not { Length: > 0 } stamp)
        {
            context.Fail("Token has no subject or stamp.");
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IUserSessionValidator>();
        if (!await validator.IsValidAsync(userId, stamp, context.HttpContext.RequestAborted))
        {
            context.Fail("Session is no longer valid.");
        }
    }
}

/// <summary>Reads the caller from validated claims only.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId => IsAuthenticated && Guid.TryParse(User!.FindFirstValue(AppClaims.Subject), out var id) ? id : null;

    public Guid? CustomerId => IsAuthenticated && Guid.TryParse(User!.FindFirstValue(AppClaims.CustomerId), out var id) ? id : null;

    public bool IsAdmin => IsAuthenticated && User!.IsInRole(Roles.Admin);

    public string AuditName => accessor.HttpContext is null ? "system" : UserId?.ToString() ?? "anonymous";
}