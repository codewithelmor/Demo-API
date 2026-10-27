


using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static DemoApi.ArchitectureTests.ArchitectureModel;

namespace DemoApi.ArchitectureTests;

/// <summary>A1-A5 (solution-structure §2, §8).</summary>
public sealed class LayerTests
{
    [Fact]
    public void A1_SharedKernel_references_only_the_bcl()
    {
        var references = SharedKernelAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.Where(n => !(n.StartsWith("System", StringComparison.Ordinal) || n is "netstandard" or "mscorlib"))
            .ShouldBeEmpty("SharedKernel is BCL only (A1)");
    }

    [Fact]
    public void A2_BuildingBlocks_has_no_domain_references() =>
        Types().That().Are(BuildingBlocksLayer)
            .Should().NotDependOnAny(DomainLayer)
            .AndShould().NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .Because("building blocks are reusable in the template (P7, A2)")
            .Check(Architecture);

    [Fact]
    public void A3_Domain_depends_only_on_SharedKernel() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .AndShould().NotDependOnAny(BuildingBlocksLayer)
            .AndShould().NotDependOnAny(EfCore)
            .AndShould().NotDependOnAny(AspNetCore)
            .AndShould().NotDependOnAny(FastEndpointsTypes)
            .AndShould().NotDependOnAny(DapperTypes)
            .AndShould().NotDependOnAny(NpgsqlDriver)
            .Because("the domain holds business rules only (A3)")
            .Check(Architecture);

    [Fact]
    public void A4_Application_does_not_depend_on_outer_layers_or_web_frameworks() =>
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .AndShould().NotDependOnAny(BuildingBlocksLayer)
            .AndShould().NotDependOnAny(AspNetCore)
            .AndShould().NotDependOnAny(NpgsqlDriver)
            .Because("use cases do not know about HTTP or the database driver (A4)")
            .Check(Architecture);

    [Fact]
    public void A4_Application_references_only_the_messaging_core_of_FastEndpoints()
    {
        var references = ApplicationAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.ShouldNotContain("FastEndpoints", "handlers use FastEndpoints.Messaging.Core only (ADR-0003)");
        references.ShouldNotContain(n => n.StartsWith("Npgsql", StringComparison.Ordinal));
        references.ShouldNotContain(n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        references.ShouldNotContain(n => n.StartsWith("DemoApi.Infrastructure", StringComparison.Ordinal) || n.StartsWith("DemoApi.BuildingBlocks", StringComparison.Ordinal));
    }

    [Fact]
    public void A5_Api_features_do_not_use_infrastructure_or_ef_core() =>
        Types().That().Are(ApiFeatures)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(EfCore)
            .Because("endpoints call Application handlers, which own data access (A5)")
            .Check(Architecture);

    [Fact]
    public void A5_Api_features_use_no_domain_entities_only_enums() =>
        Types().That().Are(ApiFeatures)
            .Should().NotDependOnAny(Classes().That().ResideInNamespaceMatching(@"^DemoApi\.Domain(\..+)?$").As("Domain classes"))
            .Because("endpoints never see entities; domain enums are allowed (solution-structure §2)")
            .Check(Architecture);

    [Fact]
    public void Infrastructure_does_not_depend_on_Api() =>
        Types().That().Are(InfrastructureLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Check(Architecture);
}