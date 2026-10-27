





using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static DemoApi.ArchitectureTests.ArchitectureModel;

using Assembly = System.Reflection.Assembly;

namespace DemoApi.ArchitectureTests;

/// <summary>A6-A8 and the cache rule (FR-130).</summary>
public sealed class SliceAndHandlerTests
{
    private static readonly string[] FeatureFolders = ["Products", "Customers", "Orders", "Reports", "Auth", "AdminUsers", "Health"];

    public static TheoryData<string, string> FeaturesPerLayer()
    {
        var data = new TheoryData<string, string>();
        foreach (var feature in FeatureFolders)
        {
            data.Add("Application", feature);
            data.Add("Api", feature);
        }

        return data;
    }

    [Fact]
    public void A6_Every_feature_folder_is_listed()
    {
        var folders = ApplicationAssembly.GetTypes().Concat(ApiAssembly.GetTypes())
            .Select(t => t.Namespace ?? string.Empty)
            .Select(ns => System.Text.RegularExpressions.Regex.Match(ns, @"^DemoApi\.(Application|Api)\.Features\.([A-Za-z]+)"))
            .Where(m => m.Success)
            .Select(m => m.Groups[2].Value)
            .Distinct();

        folders.ShouldBeSubsetOf(FeatureFolders);
    }

    /// <summary>A feature folder (with its sub-namespaces) never uses another feature folder of the same layer.</summary>
    [Theory]
    [MemberData(nameof(FeaturesPerLayer))]
    public void A6_Feature_folders_do_not_depend_on_each_other(string layer, string feature)
    {
        var others = string.Join('|', FeatureFolders.Where(f => f != feature));
        var self = Types().That().ResideInNamespaceMatching($@"^DemoApi\.{layer}\.Features\.{feature}(\..+)?$");
        var rule = Types().That().ResideInNamespaceMatching($@"^DemoApi\.{layer}\.Features\.{feature}(\..+)?$")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching($@"^DemoApi\.{layer}\.Features\.({others})(\..+)?$").As("other feature folders"))
            .Because("shared code goes to Domain or Common (A6)");

        if (self.GetObjects(Architecture).Any())
        {
            rule.Check(Architecture);
        }
    }

    [Fact]
    public void A7_Handlers_are_sealed_named_Handler_and_live_in_Application_features()
    {
        var offenders = HandlerTypes()
            .Where(t => !t.IsSealed || !t.Name.EndsWith("Handler", StringComparison.Ordinal)
                || t.Namespace?.StartsWith("DemoApi.Application.Features.", StringComparison.Ordinal) != true)
            .Select(t => t.FullName)
            .ToList();

        HandlerTypes().ShouldNotBeEmpty();
        offenders.ShouldBeEmpty("handlers are sealed, end with Handler and live in Application.Features (A7)");
    }

    [Fact]
    public void A8_Each_command_has_exactly_one_handler()
    {
        var commands = ApplicationAssembly.GetTypes()
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(IsCommandInterface))
            .ToList();
        var handled = ApplicationAssembly.GetTypes()
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)))
            .Select(i => i.GetGenericArguments()[0])
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        commands.ShouldNotBeEmpty();
        commands.Where(c => handled.GetValueOrDefault(c) != 1).Select(c => c.FullName).ShouldBeEmpty("each ICommand<T> has exactly one handler (A8)");
    }

    [Fact]
    public void Only_products_reads_the_cache_others_invalidate_through_ProductCache() =>
        Types().That().Are(ApplicationLayer)
            .And().DoNotResideInNamespaceMatching(@"^DemoApi\.Application\.(Features\.Products|Common)(\..+)?$")
            .And().DoNotHaveFullName(typeof(DemoApi.Application.Features.Orders.OrderWriter).FullName!)
            .Should().NotDependOnAny(Types(true).That().HaveFullName(typeof(IAppCache).FullName!).As("IAppCache"))
            .Because("only Products uses the cache (FR-130); OrderWriter only passes it to ProductCache.InvalidateAsync for D15")
            .Check(Architecture);

    private static bool IsCommandInterface(Type i) =>
        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>);

    private static List<Type> HandlerTypes() =>
        ApplicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType
                && (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) || i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))))
            .ToList();
}

/// <summary>A13 (domain model) and A10/A11 backups (banned calls, read from IL).</summary>
public sealed class DomainModelTests
{
    private static readonly Type[] Entities = DomainAssembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(DemoApi.SharedKernel.Domain.Entity).IsAssignableFrom(t))
        .ToArray();

    [Fact]
    public void A13_Domain_entities_have_no_public_setters()
    {
        Entities.ShouldNotBeEmpty();
        Entities.Concat([typeof(DemoApi.SharedKernel.Domain.Entity), typeof(DemoApi.SharedKernel.Domain.AuditableEntity)])
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.SetMethod is { IsPublic: true } && !IsInitOnly(p))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ShouldBeEmpty("state changes go through domain methods (A13)");
    }

    [Fact]
    public void A13_Every_business_entity_is_auditable_and_deletable() =>
        Entities.Where(t => !typeof(DemoApi.SharedKernel.Domain.IAuditable).IsAssignableFrom(t) || !typeof(DemoApi.SharedKernel.Domain.IDeletable).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ShouldBeEmpty("ADR-0006: every business entity is audited and soft deleted (A13)");

    [Theory]
    [InlineData("System.DateTime", "get_Now")]
    [InlineData("System.DateTime", "get_UtcNow")]
    [InlineData("System.DateTimeOffset", "get_Now")]
    [InlineData("System.DateTimeOffset", "get_UtcNow")]
    [InlineData("System.Threading.Thread", "Sleep")]
    [InlineData("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "ExecuteDelete")]
    [InlineData("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "ExecuteDeleteAsync")]
    [InlineData("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "ExecuteUpdate")]
    [InlineData("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "ExecuteUpdateAsync")]
    public void A10_A11_Banned_members_are_never_called(string declaringType, string member) =>
        IlScanner.CallersOf(Production, declaringType, member).ShouldBeEmpty($"{declaringType}.{member} is banned (A10/A11, FR-135, D11)");

    private static bool IsInitOnly(PropertyInfo p) =>
        p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}

/// <summary>Finds call sites in method bodies (backup for BannedApiAnalyzers).</summary>
internal static class IlScanner
{
    public static List<string> CallersOf(IEnumerable<Assembly> assemblies, string declaringType, string member)
    {
        var callers = new List<string>();
        foreach (var assembly in assemblies)
        {
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly.Location);
            foreach (var type in module.GetTypes())
            {
                foreach (var method in type.Methods.Where(m => m.HasBody))
                {
                    if (method.Body.Instructions.Any(i => i.Operand is Mono.Cecil.MethodReference r
                        && r.Name == member && r.DeclaringType.FullName == declaringType))
                    {
                        callers.Add($"{type.FullName}::{method.Name}");
                    }
                }
            }
        }

        return callers;
    }
}