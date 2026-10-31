namespace DemoApi.Bff.Proxy;

/// <summary>Static YARP configuration: the allow-list routes and one cluster pointing at the API (read lazily from options).</summary>
internal sealed class ProxyConfigProvider(IOptions<BffOptions> options) : IProxyConfigProvider
{
    private readonly Lazy<IProxyConfig> config = new(() => new StaticConfig(options.Value));

    public IProxyConfig GetConfig() => config.Value;

    private sealed class StaticConfig : IProxyConfig
    {
        public StaticConfig(BffOptions options)
        {
            Routes = AllowList.Routes();
            Clusters =
            [
                new ClusterConfig
                {
                    ClusterId = AllowList.ClusterId,
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                    {
                        ["api"] = new DestinationConfig { Address = options.ApiBaseUrl.GetLeftPart(UriPartial.Authority) + "/" },
                    },
                    HttpRequest = new Yarp.ReverseProxy.Forwarder.ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(60) },
                },
            ];
            ChangeToken = new CancellationChangeToken(CancellationToken.None);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }
    }
}