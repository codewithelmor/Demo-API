using System.Net;
using System.Text.RegularExpressions;

using DemoApi.Bff.Tests.Support;

namespace DemoApi.Bff.Tests.Bff;

public sealed partial class SpaNonceTests : BffTestBase
{
    [GeneratedRegex("name=\"csp-nonce\" content=\"([^\"]+)\"")]
    private static partial Regex MetaNonce();

    [GeneratedRegex("style-src 'self' 'nonce-([A-Za-z0-9_-]+)'")]
    private static partial Regex HeaderNonce();

    private static async Task<(string Nonce, string Html, HttpResponseMessage Response)> PageAsync(BffClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string html = await response.Content.ReadAsStringAsync();
        string csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Match header = HeaderNonce().Match(csp);
        header.Success.ShouldBeTrue(csp);
        MetaNonce().Match(html).Groups[1].Value.ShouldBe(header.Groups[1].Value); // header and meta carry the same nonce
        return (header.Groups[1].Value, html, response);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/products/42")]
    [InlineData("/index.html")]
    [InlineData("/INDEX.HTML")]
    public async Task Index_pages_carry_a_nonce_that_matches_the_csp_header(string path)
    {
        (string nonce, string html, HttpResponseMessage response) = await PageAsync(Client, path);
        using (response)
        {
            nonce.Length.ShouldBeGreaterThanOrEqualTo(22); // 128 bits, base64url
            html.ShouldNotContain(SpaNonce.Placeholder);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
            response.Headers.CacheControl!.NoStore.ShouldBeTrue();
            response.Headers.CacheControl.NoCache.ShouldBeFalse();
            string csp = response.Headers.GetValues("Content-Security-Policy").Single();
            csp.ShouldContain("script-src 'self';"); // no inline script, no nonce for scripts
            csp.ShouldNotContain("unsafe-inline");
            csp.ShouldNotContain("unsafe-eval");
            csp.ShouldContain("default-src 'self'");
            csp.ShouldContain("frame-ancestors 'none'");
        }
    }

    [Fact]
    public async Task Every_response_gets_a_fresh_nonce()
    {
        HashSet<string> seen = [];
        for (int i = 0; i < 20; i++)
        {
            (string nonce, _, HttpResponseMessage response) = await PageAsync(Client, i % 2 == 0 ? "/" : "/orders");
            response.Dispose();
            seen.Add(nonce).ShouldBeTrue("nonce was reused");
        }
    }

    [Fact]
    public async Task Head_gets_headers_with_a_nonce_and_no_body_and_get_matches_its_length_shape()
    {
        using HttpRequestMessage head = new(HttpMethod.Head, "/products/1");
        using HttpClient raw = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using HttpResponseMessage response = await raw.SendAsync(head);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        HeaderNonce().IsMatch(response.Headers.GetValues("Content-Security-Policy").Single()).ShouldBeTrue();
        (response.Content.Headers.ContentLength ?? 0).ShouldBeGreaterThan(0);

        (_, string html, HttpResponseMessage get) = await PageAsync(Client, "/products/1");
        using (get)
        {
            get.Content.Headers.ContentLength.ShouldBe(response.Content.Headers.ContentLength); // fixed-size nonce
            html.Length.ShouldBe((int)response.Content.Headers.ContentLength!.Value);
        }
    }

    [Fact]
    public async Task Non_html_responses_keep_style_src_self()
    {
        BffClient client = await SignInAsync();
        foreach (string url in new[] { "/bff/user", "/api/v1/products", "/api/v1/nope", "/assets/missing.js", "/scalar", "/bff/health/live" })
        {
            using HttpResponseMessage r = await client.GetAsync(url);
            string csp = r.Headers.GetValues("Content-Security-Policy").Single();
            csp.ShouldContain("style-src 'self';");
            csp.ShouldNotContain("nonce");
            csp.ShouldNotContain("unsafe-inline");
        }
    }

    [Fact]
    public async Task A_static_asset_is_served_unchanged_with_style_src_self()
    {
        string assets = Path.Combine(Factory.WebRoot, "assets");
        _ = Directory.CreateDirectory(assets);
        await File.WriteAllTextAsync(Path.Combine(assets, "app-1a2b3c.js"), "console.log('__CSP_NONCE__');");

        using HttpResponseMessage response = await Client.GetAsync("/assets/app-1a2b3c.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("__CSP_NONCE__"); // assets are not templated
        response.Headers.CacheControl!.ToString().ShouldContain("immutable");
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldNotContain("nonce");
    }

    [Fact]
    public async Task The_nonce_is_not_logged_and_the_full_header_set_is_still_there()
    {
        (string nonce, _, HttpResponseMessage response) = await PageAsync(Client, "/");
        using (response)
        {
            response.Headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY");
            response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
            response.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
            response.Headers.Contains("Permissions-Policy").ShouldBeTrue();
            response.Headers.GetValues("Cross-Origin-Opener-Policy").Single().ShouldBe("same-origin");
            response.Headers.Contains("Server").ShouldBeFalse();
        }

        Factory.Logs.Entries.Select(e => e.Message).ShouldNotContain(m => m.Contains(nonce, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_spa_build_is_404_and_never_leaks_a_placeholder()
    {
        File.Delete(Path.Combine(Factory.WebRoot, "index.html"));

        using HttpResponseMessage response = await Client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(SpaNonce.Placeholder);
    }
}

// Mirrors the BFF constant so a rename there fails here.
internal static class SpaNonce
{
    public const string Placeholder = DemoApi.Bff.Spa.SpaIndex.Placeholder;
}