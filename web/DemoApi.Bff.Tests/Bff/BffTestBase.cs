using System.Net;

using DemoApi.Bff.Tests.Support;

namespace DemoApi.Bff.Tests.Bff;

public abstract class BffTestBase : IDisposable
{
    protected BffTestBase()
    {
        Factory = new BffFactory();
    }

    private BffClient? client;

    protected BffFactory Factory { get; }

    // Created on first use, so a test can change Factory.Settings or Factory.Customize first.
    protected BffClient Client => client ??= Factory.NewClient();

    protected FakeApi Api => Factory.Api;

    protected async Task<BffClient> SignInAsync(string email = FakeApi.CustomerEmail)
    {
        BffClient client = Factory.NewClient();
        using HttpResponseMessage response = await client.LoginAsync(email, FakeApi.Password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.Cookie.ShouldNotBeNull();
        return client;
    }

    protected static async Task<string> TextOf(HttpResponseMessage response) =>
        response.ToString() + await response.Content.ReadAsStringAsync();

    protected static async Task<System.Text.Json.JsonElement> JsonOf(HttpResponseMessage response)
    {
        using System.Text.Json.JsonDocument doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.Clone();
    }

    public void Dispose()
    {
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}