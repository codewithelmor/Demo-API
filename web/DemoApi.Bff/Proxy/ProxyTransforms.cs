namespace DemoApi.Bff.Proxy;

/// <summary>Request and response transforms applied to every allow-listed route (NFR-F32, NFR-F33).</summary>
internal static class ProxyTransforms
{
    // The only browser request headers the API gets (allow-list, L-8). Everything else, including Cookie,
    // Authorization, Forwarded, X-Forwarded-*, X-Real-IP, True-Client-IP, Via, Referer, Origin, X-CSRF and
    // method-override headers, is dropped. The Bearer token and traceparent are added by the BFF.
    private static readonly string[] AllowedRequestHeaders =
    [
        "Accept",
        "Accept-Language",
        "If-Match",
        "If-None-Match",
        "User-Agent",
    ];

    private static readonly string[] AllowedContentHeaders = ["Content-Type", "Content-Length"];

    private static readonly string[] StrippedResponseHeaders = ["Set-Cookie", "Server"];

    public static void Configure(TransformBuilderContext context)
    {
        // YARP would add X-Forwarded-*; the API ignores them (T-31) and a spoofed value must never pass.
        context.UseDefaultForwarders = false;
        context.CopyRequestHeaders = false;

        bool anonymous = context.Route.Metadata?.ContainsKey(AllowList.AnonymousMetadata) == true;
        bool endsSession = context.Route.Metadata?.ContainsKey(AllowList.EndsSessionMetadata) == true;

        context.AddRequestTransform(transform =>
        {
            HttpRequestHeaders headers = transform.ProxyRequest.Headers;
            IHeaderDictionary incoming = transform.HttpContext.Request.Headers;
            foreach (string name in AllowedRequestHeaders)
            {
                if (incoming.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues value))
                {
                    _ = headers.TryAddWithoutValidation(name, (IEnumerable<string>)value!);
                }
            }

            if (transform.ProxyRequest.Content is { } content)
            {
                foreach (string name in AllowedContentHeaders)
                {
                    if (incoming.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues value))
                    {
                        _ = content.Headers.TryAddWithoutValidation(name, (IEnumerable<string>)value!);
                    }
                }
            }

            if (!anonymous && transform.HttpContext.Items[ProxyCall.ItemsKey] is ProxyCall call)
            {
                headers.Authorization = new AuthenticationHeaderValue("Bearer", call.AccessToken);
                transform.ProxyRequest.Options.Set(ProxyCall.OptionsKey, call);
            }

            return ValueTask.CompletedTask;
        });

        context.AddResponseTransform(async transform =>
        {
            HttpResponse response = transform.HttpContext.Response;
            foreach (string name in StrippedResponseHeaders)
            {
                _ = response.Headers.Remove(name);
            }

            // The API has already revoked the tokens (EP-09, EP-37), so the session ends here too.
            if (endsSession
                && transform.ProxyResponse is { IsSuccessStatusCode: true }
                && transform.HttpContext.Items[ProxyCall.ItemsKey] is ProxyCall call)
            {
                call.Ended = true;
                SessionService sessions = transform.HttpContext.RequestServices.GetRequiredService<SessionService>();
                await sessions.DeleteQuietlyAsync(call.SessionId);
                transform.HttpContext.SecurityEvents().SessionEnded(SecurityReasons.ApiRevoked, call.SessionId, call.ClientIp);
                SessionCookie.Delete(response);
            }
        });
    }
}