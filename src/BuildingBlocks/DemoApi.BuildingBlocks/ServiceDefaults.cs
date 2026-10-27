namespace DemoApi.BuildingBlocks;

public sealed class SystemDateTimeProvider(TimeProvider time) : IDateTimeProvider
{
    public DateTimeOffset UtcNow => time.GetUtcNow();
}

/// <summary>Writes UTC with <c>Z</c>; rejects input without an offset (D8).</summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (!IsoDates.TryParse(text, out var value))
        {
            throw new JsonException("Dates need an ISO 8601 offset (Z or ±hh:mm).");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
}

public static class IsoDates
{
    /// <summary>ISO 8601 with an explicit offset only; result is UTC.</summary>
    public static bool TryParse(string? text, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text) || text.Length < 20)
        {
            return false;
        }

        var tail = text[^6..];
        var hasOffset = text.EndsWith('Z') || text.EndsWith('z') || ((tail[0] == '+' || tail[0] == '-') && tail[3] == ':');
        if (!hasOffset || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        value = parsed.ToUniversalTime();
        return true;
    }
}

public static class ServiceDefaults
{
    public const string RoutePrefix = "api/v1";

    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<PostgresCircuitBreaker>();
        services.AddScoped<AuditingInterceptor>();
        services.AddSingleton<PostgresCircuitBreakerInterceptor>();
        services.AddJwtAuth();
        services.AddAppRateLimiting();
        services.AddAppCaching(configuration);
        services.AddAppHttp(configuration);
        services.AddAppObservability(configuration);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddRequestTimeouts(o => o.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(15) });
        // Minimal-API results (TypedResults) use the same JSON rules as FastEndpoints.
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.SerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
        });
        services.OpenApiDocument(o =>
        {
            o.DocumentName = "v1";
            o.Title = "Orders API";
            o.Version = "v1";
            o.ShortSchemaNames = true;
            o.EnableJWTBearerAuth = true;
        });
        return services;
    }

    public static IServiceCollection AddTransactionalPersistence<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<ITransactionRunner, TransactionRunner<TContext>>();
        services.AddScoped<ISqlRunner, SqlRunner<TContext>>();
        return services;
    }

    /// <summary>
    /// Pipeline order (ADR-0010): exception handler → status pages → security headers → routing → CORS →
    /// authentication → rate limiter → authorization → FastEndpoints.
    /// </summary>
    public static WebApplication UseBuildingBlocks(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(Problems.WriteStatusCodePageAsync);
        app.UseMiddleware<SecurityHeadersMiddleware>();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseCors();
        app.UseRequestTimeouts();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseFastEndpoints(c =>
        {
            c.Endpoints.RoutePrefix = RoutePrefix;
            c.Endpoints.ShortNames = true;
            c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            c.Serializer.Options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
            c.Serializer.Options.Converters.Add(new UtcDateTimeOffsetConverter());
            c.Errors.ContentType = Problems.ContentType;
            c.Errors.ResponseBuilder = BuildValidationProblem;
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous().RateLimit(RateLimitPolicies.None);
            app.MapScalarApiReference().AllowAnonymous().RateLimit(RateLimitPolicies.None);
            // A browser opening the root lands on the API reference instead of a 401.
            app.MapGet("/", () => Results.Redirect("/scalar")).AllowAnonymous().RateLimit(RateLimitPolicies.None).ExcludeFromDescription();
        }

        return app;
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails BuildValidationProblem(List<ValidationFailure> failures, HttpContext context, int status)
    {
        var errors = failures
            .GroupBy(f => ToCamelPath(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        context.Response.Headers.CacheControl = "no-store";
        return Problems.Create(context, status, "validation-failed", "Validation failed", "One or more fields are invalid.", errors);
    }

    private static string ToCamelPath(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "GeneralErrors" || name == "SerializerErrors")
        {
            return "body";
        }

        return string.Join('.', name.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
    }
}