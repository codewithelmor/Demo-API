





using static DemoApi.ArchitectureTests.ArchitectureModel;

namespace DemoApi.ArchitectureTests;

/// <summary>Builds the app without a database: no hosted seeding, a dummy connection string.</summary>
public sealed class NoDatabaseFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none");
        builder.UseSetting("ConnectionStrings:Valkey", string.Empty);
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("SeedAdmin:Enabled", "false");
    }
}

/// <summary>A9 (EndpointDoDTests) and EndpointPolicyTests (test-strategy §4).</summary>
public sealed partial class EndpointDoDTests : IClassFixture<NoDatabaseFactory>
{
    private static readonly Dictionary<string, string> ExpectedRateLimitPolicy = new(StringComparer.Ordinal)
    {
        ["POST api/v1/auth/register"] = RateLimitPolicies.AuthStrict,
        ["POST api/v1/auth/confirm-email"] = RateLimitPolicies.AuthStrict,
        ["POST api/v1/auth/resend-confirmation"] = RateLimitPolicies.AuthStrict,
        ["POST api/v1/auth/login"] = RateLimitPolicies.AuthStrict,
        ["POST api/v1/auth/refresh"] = RateLimitPolicies.Refresh,
        ["POST api/v1/auth/change-password"] = RateLimitPolicies.AuthStrict,
        ["POST api/v1/auth/forgot-password"] = RateLimitPolicies.AuthStrict,
        ["POST api/v1/auth/reset-password"] = RateLimitPolicies.AuthStrict,
        ["POST api/v1/customers/me/erase"] = RateLimitPolicies.AuthStrict,
        ["GET health/live"] = RateLimitPolicies.None,
        ["GET health/ready"] = RateLimitPolicies.None,
    };

    private readonly List<(string Key, EndpointDefinition Definition, Microsoft.AspNetCore.Http.Endpoint Endpoint)> _endpoints;

    public EndpointDoDTests(NoDatabaseFactory factory)
    {
        var source = factory.Services.GetRequiredService<EndpointDataSource>();
        _endpoints = source.Endpoints.OfType<RouteEndpoint>()
            .Select(e => (e, d: e.Metadata.GetMetadata<EndpointDefinition>()))
            .Where(x => x.d is not null)
            .Select(x => ($"{x.e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods[0]} {x.e.RoutePattern.RawText!.TrimStart('/')}", x.d!, (Microsoft.AspNetCore.Http.Endpoint)x.e))
            .ToList();
    }

    [Fact]
    public void All_core_endpoints_are_registered() => _endpoints.Count.ShouldBe(51);

    [Fact]
    public void A9_Every_endpoint_declares_an_explicit_auth_rule() =>
        _endpoints.Where(e => (e.Definition.AnonymousVerbs?.Length ?? 0) == 0
                && (e.Definition.PreBuiltUserPolicies?.Count ?? 0) == 0
                && (e.Definition.AllowedRoles?.Count ?? 0) == 0)
            .Select(e => e.Key)
            .ShouldBeEmpty("Roles/Policies/AllowAnonymous is required on every endpoint (A9)");

    [Fact]
    public void A9_Every_endpoint_has_an_openapi_summary_and_description() =>
        _endpoints.Where(e => string.IsNullOrWhiteSpace(e.Definition.EndpointSummary?.Summary)
                || string.IsNullOrWhiteSpace(e.Definition.EndpointSummary?.Description))
            .Select(e => e.Key)
            .ShouldBeEmpty("P5, NFR-17 (A9)");

    /// <summary>
    /// A request needs a validator when it has a property the binder alone cannot fully check. Route
    /// GUIDs, booleans and the If-Match header are complete after binding (malformed = 400 from binding).
    /// </summary>
    [Fact]
    public void A9_Every_request_with_checkable_input_has_a_validator() =>
        _endpoints.Where(e => e.Definition.ValidatorType is null && NeedsValidator(e.Definition))
            .Select(e => $"{e.Key} ({e.Definition.ReqDtoType.Name})")
            .ShouldBeEmpty("validator required when the request has properties (A9)");

    [Fact]
    public void Rate_limit_policy_matches_the_endpoint_catalog() =>
        _endpoints.Select(e => (e.Key,
                Actual: e.Endpoint.Metadata.GetMetadata<RateLimitPolicyMetadata>()?.Policy ?? RateLimitPolicies.General,
                Expected: ExpectedRateLimitPolicy.GetValueOrDefault(e.Key, RateLimitPolicies.General)))
            .Where(x => x.Actual != x.Expected)
            .Select(x => $"{x.Key}: {x.Actual} (expected {x.Expected})")
            .ShouldBeEmpty("endpoint catalog §1.11");

    [Fact]
    public void Business_routes_use_the_api_v1_prefix_and_no_guid_constraint() =>
        _endpoints.Select(e => e.Key)
            .Where(k => !(k.Contains(" api/v1/", StringComparison.Ordinal) || k.Contains(" health/", StringComparison.Ordinal)) || k.Contains(":guid", StringComparison.Ordinal))
            .ShouldBeEmpty("catalog §1.1 (D7)");

    private static bool NeedsValidator(EndpointDefinition definition)
    {
        var type = definition.ReqDtoType;
        if (type is null || type == typeof(EmptyRequest))
        {
            return false;
        }

        var routeParams = definition.Routes.SelectMany(r => RouteParam().Matches(r).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => !routeParams.Contains(p.Name)
                && p.GetCustomAttribute<FromHeaderAttribute>() is null
                && Nullable.GetUnderlyingType(p.PropertyType) != typeof(bool)
                && p.PropertyType != typeof(bool));
    }

    [GeneratedRegex(@"\{([A-Za-z0-9_]+)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex RouteParam();
}

/// <summary>A12 (no entity in responses) and A14 (DateTimeOffset only in contracts).</summary>
public sealed class ContractTests
{
    private static readonly Type[] EndpointTypes = ApiAssembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(BaseEndpoint).IsAssignableFrom(t))
        .ToArray();

    [Fact]
    public void A12_No_domain_entity_is_reachable_from_a_response_type()
    {
        var responses = EndpointTypes.SelectMany(GenericArguments).Distinct().ToList();
        responses.AddRange(ApplicationAssembly.GetTypes().Where(t => t.IsPublic && (t.Name.EndsWith("Response", StringComparison.Ordinal) || t.Name.EndsWith("Item", StringComparison.Ordinal))));

        responses.SelectMany(t => Walk(t, [])).Where(IsEntity).Select(t => t.FullName).Distinct()
            .ShouldBeEmpty("never return entities from endpoints (A12)");
    }

    [Fact]
    public void A14_Contracts_use_DateTimeOffset_not_DateTime()
    {
        var contracts = EndpointTypes.SelectMany(GenericArguments).Distinct().ToList();

        contracts.SelectMany(t => Walk(t, []))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ShouldBeEmpty("FR-125 (A14)");
    }

    private static IEnumerable<Type> GenericArguments(Type endpoint)
    {
        for (var t = endpoint; t is not null; t = t.BaseType)
        {
            if (t.IsGenericType)
            {
                foreach (var arg in t.GetGenericArguments())
                {
                    yield return arg;
                }
            }
        }
    }

    private static bool IsEntity(Type t) => typeof(DemoApi.SharedKernel.Domain.Entity).IsAssignableFrom(t);

    private static IEnumerable<Type> Walk(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type) || type.Namespace?.StartsWith("DemoApi", StringComparison.Ordinal) != true && !type.IsGenericType)
        {
            yield break;
        }

        yield return type;
        var children = type.IsGenericType ? type.GetGenericArguments().AsEnumerable() : [];
        children = children.Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.PropertyType));
        foreach (var child in children)
        {
            var element = child.IsArray ? child.GetElementType()! : child;
            foreach (var t in Walk(element, seen))
            {
                yield return t;
            }
        }
    }
}