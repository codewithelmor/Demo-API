namespace DemoApi.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers query handlers (injected into endpoints) and shared Application services.
    /// Command handlers are found by the FastEndpoints command bus.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton(settings);
        services.AddScoped<OrderWriter>();

        var handlers = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });
        foreach (var type in handlers)
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)))
            {
                services.AddScoped(contract, type);
            }
        }

        return services;
    }
}