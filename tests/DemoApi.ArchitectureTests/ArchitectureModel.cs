using ArchUnitNET.Domain;


using static ArchUnitNET.Fluent.ArchRuleDefinition;

using Assembly = System.Reflection.Assembly;

namespace DemoApi.ArchitectureTests;

/// <summary>Loads every production assembly once (solution-structure §8).</summary>
internal static class ArchitectureModel
{
    public static readonly Assembly SharedKernelAssembly = typeof(DemoApi.SharedKernel.Results.Result).Assembly;
    public static readonly Assembly BuildingBlocksAssembly = typeof(DemoApi.BuildingBlocks.ServiceDefaults).Assembly;
    public static readonly Assembly DomainAssembly = typeof(DemoApi.Domain.Products.Product).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(DemoApi.Application.DependencyInjection).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(DemoApi.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    public static readonly Assembly[] Production = [SharedKernelAssembly, BuildingBlocksAssembly, DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly];

    public static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(Production).Build();

    public static IObjectProvider<IType> Namespace(string regex, string name) =>
        Types().That().ResideInNamespaceMatching(regex).As(name);

    public static IObjectProvider<IType> Framework(string regex, string name) =>
        Types(true).That().ResideInNamespaceMatching(regex).As(name);

    public static readonly IObjectProvider<IType> SharedKernelLayer = Namespace(@"^DemoApi\.SharedKernel(\..+)?$", "SharedKernel");
    public static readonly IObjectProvider<IType> BuildingBlocksLayer = Namespace(@"^DemoApi\.BuildingBlocks(\..+)?$", "BuildingBlocks");
    public static readonly IObjectProvider<IType> DomainLayer = Namespace(@"^DemoApi\.Domain(\..+)?$", "Domain");
    public static readonly IObjectProvider<IType> ApplicationLayer = Namespace(@"^DemoApi\.Application(\..+)?$", "Application");
    public static readonly IObjectProvider<IType> InfrastructureLayer = Namespace(@"^DemoApi\.Infrastructure(\..+)?$", "Infrastructure");
    public static readonly IObjectProvider<IType> ApiLayer = Namespace(@"^DemoApi\.Api(\..+)?$", "Api");
    public static readonly IObjectProvider<IType> ApiFeatures = Namespace(@"^DemoApi\.Api\.Features(\..+)?$", "Api features");

    public static readonly IObjectProvider<IType> EfCore = Framework(@"^Microsoft\.EntityFrameworkCore(\..+)?$", "EF Core");
    public static readonly IObjectProvider<IType> AspNetCore = Framework(@"^Microsoft\.AspNetCore(\..+)?$", "ASP.NET Core");
    public static readonly IObjectProvider<IType> NpgsqlDriver = Framework(@"^Npgsql(\..+)?$", "Npgsql");
    public static readonly IObjectProvider<IType> DapperTypes = Framework(@"^Dapper(\..+)?$", "Dapper");
    public static readonly IObjectProvider<IType> FastEndpointsTypes = Framework(@"^FastEndpoints(\..+)?$", "FastEndpoints");
}