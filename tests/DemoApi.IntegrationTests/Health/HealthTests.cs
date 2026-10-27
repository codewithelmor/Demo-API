namespace DemoApi.IntegrationTests.Health;

public sealed class HealthTests(IntegrationFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    [Endpoint("EP-56")]
    public async Task Live_returns_healthy_without_a_token()
    {
        var response = await Anonymous().GetAsync("/health/live", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.JsonAsync())["status"]!.GetValue<string>().ShouldBe("Healthy");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    [Endpoint("EP-57")]
    [Story("US-219", "AC1")]
    public async Task Ready_is_healthy_and_hides_details_outside_development()
    {
        var response = await Anonymous().GetAsync("/health/ready", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = (await response.JsonAsync()).AsObject();
        body["status"]!.GetValue<string>().ShouldBe("Healthy");
        body.ContainsKey("checks").ShouldBeFalse("details are Development only (D10)");
    }

    [Fact]
    [Endpoint("EP-56")]
    public async Task Invalid_bearer_token_is_ignored_on_probes()
    {
        var response = await Factory.CreateApiClient("not-a-jwt").GetAsync("/health/live", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    [Security("ST-10")]
    public async Task Responses_carry_security_headers_and_no_server_header()
    {
        var response = await Anonymous().GetAsync("/health/live", Ct);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.Contains("Server").ShouldBeFalse();
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_details_with_trace_id()
    {
        var admin = await AdminAsync();

        await (await admin.Client.GetAsync("/api/v1/does-not-exist", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    [Security("ST-01")]
    public async Task Unknown_route_without_a_token_returns_401_from_the_fallback_policy() =>
        await (await Anonymous().GetAsync("/api/v1/does-not-exist", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
}