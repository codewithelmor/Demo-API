namespace DemoApi.IntegrationTests.Security;

/// <summary>T-30: the API reference and the OpenAPI document exist in Development only. The test host runs as "Testing", not Development.</summary>
public sealed class NonDevelopmentTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    [Theory]
    [InlineData("/scalar")]
    [InlineData("/openapi/v1.json")]
    [Security("ST-10")]
    public async Task Api_reference_is_not_exposed_outside_development(string path)
    {
        var response = await Anonymous().GetAsync(path, Ct);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized); // the fallback policy answers 401 before routing finds nothing
    }
}