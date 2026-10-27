namespace DemoApi.IntegrationTests.Infrastructure;

/// <summary>Assertions on raw HTTP and JSON (not on our own DTO types), so renamed properties are caught.</summary>
public static class HttpAssertions
{
    public static async Task<JsonNode> JsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text) ?? throw new ShouldAssertException("Empty JSON body.");
    }

    public static async Task ShouldHaveStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: expected {(int)expected} but got {(int)response.StatusCode}. Body: {body}");
        }
    }

    /// <summary>RFC 9457 shape with a /problems/{slug} type, the request path as instance and a traceId (FR-124).</summary>
    public static async Task<JsonObject> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string? slug = null)
    {
        await response.ShouldHaveStatusAsync(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.CacheControl?.NoStore.ShouldBeTrue("errors are never cached (NFR-06)");
        var problem = (await response.JsonAsync()).AsObject();
        problem["status"]!.GetValue<int>().ShouldBe((int)status);
        problem["type"]!.GetValue<string>().ShouldStartWith("/problems/");
        problem["title"].ShouldNotBeNull();
        problem["traceId"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        problem["instance"]!.GetValue<string>().ShouldBe(response.RequestMessage!.RequestUri!.AbsolutePath);
        if (slug is not null)
        {
            problem["type"]!.GetValue<string>().ShouldBe($"/problems/{slug}");
        }

        return problem;
    }

    public static async Task<JsonObject> ShouldBeValidationProblemAsync(this HttpResponseMessage response, params string[] fields)
    {
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation-failed");
        var errors = problem["errors"]!.AsObject();
        foreach (var field in fields)
        {
            errors.ContainsKey(field).ShouldBeTrue($"errors should contain '{field}' but has: {string.Join(", ", errors.Select(e => e.Key))}");
        }

        return problem;
    }

    /// <summary>EP-22/23: <c>Cache-Control: private, no-cache</c> (directive order is not significant).</summary>
    public static void ShouldHaveProductCacheControl(this HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl.ShouldNotBeNull();
        cacheControl.Private.ShouldBeTrue();
        cacheControl.NoCache.ShouldBeTrue();
        cacheControl.NoStore.ShouldBeFalse();
    }

    public static string ETag(this HttpResponseMessage response) =>
        response.Headers.ETag?.ToString()
        ?? throw new ShouldAssertException($"No ETag header on {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery} ({(int)response.StatusCode}). Headers: {response.Headers}");
}

/// <summary>Builds a JSON request body from name/value pairs (keeps credential field names out of anonymous types).</summary>
public static class Body
{
    public static Dictionary<string, object?> Of(params (string Name, object? Value)[] fields) =>
        fields.ToDictionary(f => f.Name, f => f.Value, StringComparer.Ordinal);
}