namespace DemoApi.Bff;

internal static class BffHost
{
    public static WebApplicationBuilder AddBff(this WebApplicationBuilder builder)
    {
        IServiceCollection services = builder.Services;

        // Compose dev secrets (Valkey__ConnectionString) as key-per-file config; absent outside compose.
        _ = builder.Configuration.AddKeyPerFile("/run/secrets/bff", optional: true, reloadOnChange: false);

        builder.WebHost.ConfigureKestrel(static kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = 1_048_576;
        });

        _ = services.AddOptions<BffOptions>().Bind(builder.Configuration.GetSection(BffOptions.SectionName)).ValidateOnStart();
        _ = services.AddOptions<ValkeyOptions>().Bind(builder.Configuration.GetSection(ValkeyOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BffOptions>, BffOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ValkeyOptions>, ValkeyOptionsValidator>());
        _ = services.AddOptions<KeyStoreOptions>().Bind(builder.Configuration.GetSection(KeyStoreOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<KeyStoreOptions>, KeyStoreOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.PostConfigure<LoggerFilterOptions>(EnforceQuietCategories);

        // Session store: Valkey through IDistributedCache. Process memory only when Valkey:AllowInMemory allows it
        // (checked at startup by ValkeyOptionsValidator).
        services.TryAddSingleton<IDistributedCache>(static sp =>
        {
            string? connection = sp.GetRequiredService<IOptions<ValkeyOptions>>().Value.ConnectionString;
            return string.IsNullOrWhiteSpace(connection)
                ? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))
                : new RedisCache(Options.Create(new RedisCacheOptions { ConfigurationOptions = SafeRedisOptions(connection) }));
        });

        // Data Protection keys on a file volume when configured (not in Valkey: no persistence, LRU eviction).
        _ = services.AddDataProtection().SetApplicationName("DemoApi.Bff");
        _ = services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>((keys, config, loggers) =>
        {
            string? path = config["DataProtection:KeysPath"];
            if (!string.IsNullOrWhiteSpace(path))
            {
                keys.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), loggers);
            }
        });

        services.AddSingleton<IUpstreamHandlerFactory, SocketsUpstreamHandlerFactory>();
        _ = services.AddHttpClient(ApiAuthClient.ClientName)
            .ConfigureHttpClient(static (sp, client) =>
            {
                client.BaseAddress = sp.GetRequiredService<IOptions<BffOptions>>().Value.ApiBaseUrl;
                client.Timeout = TimeSpan.FromSeconds(10);
                client.MaxResponseContentBufferSize = 256 * 1024;
            })
            .ConfigurePrimaryHttpMessageHandler(static sp => sp.GetRequiredService<IUpstreamHandlerFactory>().Create())
            .RemoveAllLoggers(); // no HttpClient logging of URLs or headers (NFR-F39)

        services.AddSingleton<SecurityLog>();
        _ = services.AddBffTelemetry(builder.Configuration);
        services.AddSingleton<ApiAuthClient>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<RefreshRateLimiter>();
        services.AddSingleton<AnonymousRateLimiter>();
        services.AddSingleton<RefreshCoordinator>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<SpaIndex>();

        _ = services.AddBffRateLimiting();
        _ = services.AddExceptionHandler<BffExceptionHandler>();
        _ = services.AddProblemDetails();

        services.AddSingleton<ReadinessCheck>();
        _ = services.AddHealthChecks().AddCheck<ReadinessCheck>("ready", tags: ["ready"]);

        _ = services.AddReverseProxy().AddTransforms(ProxyTransforms.Configure);
        services.AddSingleton<IProxyConfigProvider, ProxyConfigProvider>();
        services.AddSingleton<IForwarderHttpClientFactory, BffForwarderHttpClientFactory>();
        return builder;
    }

    // Categories that log request URLs (with the query string). They stay at Warning whatever the configuration says:
    // confirm-email and reset-password carry tokens in the query (NFR-F39, F-4). Post-configure runs after every config
    // source, and provider-specific rules (for example Console) are covered too, because they take precedence.
    private static readonly string[] QuietCategories = ["Microsoft.AspNetCore.Hosting.Diagnostics", "Yarp", "System.Net.Http.HttpClient"];

    private static void EnforceQuietCategories(LoggerFilterOptions options)
    {
        for (int i = options.Rules.Count - 1; i >= 0; i--)
        {
            string? category = options.Rules[i].CategoryName;
            if (category is not null && QuietCategories.Any(q => category.StartsWith(q, StringComparison.OrdinalIgnoreCase)))
            {
                options.Rules.RemoveAt(i);
            }
        }

        string?[] providers = [null, .. options.Rules.Select(static r => r.ProviderName).Where(static p => p is not null).Distinct()];
        foreach (string? provider in providers)
        {
            foreach (string category in QuietCategories)
            {
                options.Rules.Add(new LoggerFilterRule(provider, category, LogLevel.Warning, null));
            }
        }
    }

    // Redis errors normally name the command and key. The key holds the session ID, so details stay out of exceptions.
    internal static ConfigurationOptions SafeRedisOptions(string connection)
    {
        ConfigurationOptions options = ConfigurationOptions.Parse(connection);
        options.IncludeDetailInExceptions = false;
        options.IncludePerformanceCountersInExceptions = false;
        return options;
    }

    public static WebApplication UseBff(this WebApplication app)
    {
        _ = app.UseMiddleware<TraceMiddleware>();
        _ = app.UseMiddleware<SecurityHeadersMiddleware>();
        _ = app.UseExceptionHandler(); // ProblemDetails in every environment, no developer exception page
        if (!app.Environment.IsDevelopment())
        {
            _ = app.UseHsts(); // HTTPS only; never sent for localhost
        }

        _ = app.UseStatusCodePages(static context => Problems.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
        _ = app.UseMiddleware<PathGuardMiddleware>();
        _ = app.UseSpaStaticFiles();
        _ = app.UseRouting();
        _ = app.UseMiddleware<CsrfMiddleware>();
        _ = app.UseMiddleware<AnonymousRateLimitMiddleware>();
        _ = app.UseRateLimiter();

        _ = app.MapBffAuth();
        _ = app.MapBffHealth();
        _ = app.MapReverseProxy(static proxy =>
        {
            _ = proxy.UseMiddleware<ProxyGateMiddleware>();
            _ = proxy.UseSessionAffinity();
            _ = proxy.UseLoadBalancing();
            _ = proxy.UsePassiveHealthChecks();
        });

        // Anything else under /api is 404 ProblemDetails, never index.html.
        _ = app.Map("/api/{**rest}", static () => new ProblemResult(StatusCodes.Status404NotFound, "not-found"));
        _ = app.MapSpaFallback();
        return app;
    }
}