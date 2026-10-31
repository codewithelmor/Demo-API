namespace DemoApi.Bff.Upstream;

/// <summary>
/// Creates the primary HTTP handler for every call to the API (auth client and YARP). Tests replace it with an
/// in-process fake API.
/// </summary>
internal interface IUpstreamHandlerFactory
{
    HttpMessageHandler Create();
}

internal sealed class SocketsUpstreamHandlerFactory : IUpstreamHandlerFactory
{
    public HttpMessageHandler Create() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    };
}