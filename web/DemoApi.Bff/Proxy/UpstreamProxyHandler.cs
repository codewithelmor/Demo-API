namespace DemoApi.Bff.Proxy;

/// <summary>
/// The HTTP handler chain YARP uses to reach the API. It (1) propagates <c>traceparent</c> from the current
/// activity and (2) after an API 401 runs one single-flight refresh and replays the request once. Replay is safe
/// because the API answers 401 before any handler runs. It never replays on any other status (ADR-0015).
/// </summary>
internal sealed class UpstreamProxyHandler(HttpMessageHandler inner, RefreshCoordinator refresh) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        InjectTraceContext(request);
        if (!request.Options.TryGetValue(ProxyCall.OptionsKey, out ProxyCall? call) || call.Replayed)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Capture before sending: the content is disposed with the first request.
        List<KeyValuePair<string, IEnumerable<string>>> contentHeaders = request.Content is null ? [] : [.. request.Content.Headers];

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Only a real 401 from the API starts a refresh. A 429 or any other status is passed through.
        RefreshOutcome outcome = await refresh.EnsureFreshAsync(call.SessionId, call.AccessToken, true, call.ClientIp, cancellationToken);
        if (outcome is not RefreshOutcome.Fresh fresh)
        {
            response.Dispose();
            throw new ProxyAuthException(outcome);
        }

        response.Dispose();
        call.Replayed = true;
        call.AccessToken = fresh.Session.AccessToken;
        call.RefreshTokenExpiresAt = fresh.Session.RefreshTokenExpiresAt;

        using HttpRequestMessage replay = Clone(request, call, contentHeaders);
        return await base.SendAsync(replay, cancellationToken);
    }

    private static HttpRequestMessage Clone(
        HttpRequestMessage original,
        ProxyCall call,
        List<KeyValuePair<string, IEnumerable<string>>> contentHeaders)
    {
        HttpRequestMessage clone = new(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy,
        };
        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
        {
            _ = clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", call.AccessToken);
        clone.Options.Set(ProxyCall.OptionsKey, call);

        if (call.Body is { Length: > 0 } body)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (KeyValuePair<string, IEnumerable<string>> header in contentHeaders)
            {
                if (!string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    _ = clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        return clone;
    }

    private static void InjectTraceContext(HttpRequestMessage request)
    {
        // A trace header sent by the browser is dropped; the trace starts in the BFF.
        _ = request.Headers.Remove("traceparent");
        _ = request.Headers.Remove("tracestate");
        if (Activity.Current is { } activity)
        {
            DistributedContextPropagator.Current.Inject(
                activity,
                request,
                static (carrier, name, value) =>
                {
                    if (carrier is HttpRequestMessage message)
                    {
                        _ = message.Headers.TryAddWithoutValidation(name, value);
                    }
                });
        }
    }
}

/// <summary>Builds the YARP client on top of <see cref="IUpstreamHandlerFactory"/> so tests can swap in a fake API.</summary>
internal sealed class BffForwarderHttpClientFactory(IUpstreamHandlerFactory upstream, RefreshCoordinator refresh) : IForwarderHttpClientFactory
{
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) =>
        new(new UpstreamProxyHandler(upstream.Create(), refresh), disposeHandler: true);
}