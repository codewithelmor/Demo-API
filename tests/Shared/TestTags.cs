using Xunit.v3;

namespace DemoApi.Tests;

/// <summary>Traceability tag: story and acceptance criterion (test-strategy section 7).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class StoryAttribute(string story, string acceptanceCriterion) : Attribute, ITraitAttribute
{
    public string Story { get; } = story;

    public string AcceptanceCriterion { get; } = acceptanceCriterion;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new("Story", Story), new("AC", $"{Story} {AcceptanceCriterion}")];
}

/// <summary>Traceability tag: endpoint ID from the endpoint catalog.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class EndpointAttribute(string endpointId) : Attribute, ITraitAttribute
{
    public string EndpointId { get; } = endpointId;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new("Endpoint", EndpointId)];
}

/// <summary>Traceability tag: security test ID (threat model section 6).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class SecurityAttribute(string securityTestId) : Attribute, ITraitAttribute
{
    public string SecurityTestId { get; } = securityTestId;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new("Security", SecurityTestId)];
}

/// <summary>Marks a test that asserts the audit row of a write endpoint (AuditCoverageTests).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class AuditAttribute(string endpointId) : Attribute, ITraitAttribute
{
    public string EndpointId { get; } = endpointId;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new("Audit", EndpointId)];
}