namespace DemoApi.Bff.Spa;

/// <summary>
/// The SPA entry page. index.html is read once and kept in memory as a template. Every response gets a fresh
/// 128-bit nonce in place of <c>__CSP_NONCE__</c>, and the CSP header of that response allows exactly that nonce
/// for styles (Radix injects a style element). Scripts stay <c>'self'</c> with no inline code. The page stays
/// <c>no-store</c>, so a nonce is never reused from a cache. The nonce is never logged.
/// </summary>
internal sealed class SpaIndex(IWebHostEnvironment environment)
{
    public const string Placeholder = "__CSP_NONCE__";
    public const string NonceItemKey = "bff.csp-nonce";

    private string? template;

    /// <summary>Writes index.html with a fresh nonce. Returns false when the SPA is not built into wwwroot.</summary>
    public async Task<bool> TryWriteAsync(HttpContext context)
    {
        string? html = Template();
        if (html is null)
        {
            return false;
        }

        string nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceItemKey] = nonce; // read by the security headers when the response starts
        byte[] body = Encoding.UTF8.GetBytes(html.Replace(Placeholder, nonce, StringComparison.Ordinal));

        HttpResponse response = context.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/html; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        response.ContentLength = body.Length;
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await response.Body.WriteAsync(body, context.RequestAborted);
        }

        return true;
    }

    private string? Template()
    {
        if (template is not null)
        {
            return template;
        }

        IFileInfo file = environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!file.Exists)
        {
            return null;
        }

        using StreamReader reader = new(file.CreateReadStream(), Encoding.UTF8);
        template = reader.ReadToEnd();
        return template;
    }
}

/// <summary>Serves /index.html through <see cref="SpaIndex"/>, so the raw placeholder file is never sent as a static file.</summary>
internal sealed class IndexHtmlMiddleware(RequestDelegate next, SpaIndex index)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && string.Equals(context.Request.Path.Value, "/index.html", StringComparison.OrdinalIgnoreCase)
            && await index.TryWriteAsync(context))
        {
            return;
        }

        await next(context);
    }
}

/// <summary>Serves the built SPA from wwwroot. index.html is the fallback for GET outside /api and /bff only.</summary>
internal static class SpaHosting
{
    // Never the SPA: these answer 404 here (FT-04), not index.html.
    private static readonly string[] ReservedPrefixes = ["/scalar", "/openapi", "/health"];

    public static IApplicationBuilder UseSpaStaticFiles(this IApplicationBuilder app)
    {
        _ = app.UseMiddleware<IndexHtmlMiddleware>();
        return app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = static context =>
            {
                bool hashed = context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal);
                context.Context.Response.Headers.CacheControl = hashed ? "public, max-age=31536000, immutable" : "no-cache";
            },
        });
    }

    public static IEndpointRouteBuilder MapSpaFallback(this IEndpointRouteBuilder app)
    {
        _ = app.MapMethods("/{**path}", ["GET", "HEAD"], static async (HttpContext context, SpaIndex index) =>
        {
            string path = context.Request.Path.Value ?? "/";
            string last = path[(path.LastIndexOf('/') + 1)..];
            if (last.Contains('.', StringComparison.Ordinal)
                || ReservedPrefixes.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))
                || !await index.TryWriteAsync(context))
            {
                return new ProblemResult(StatusCodes.Status404NotFound, "not-found");
            }

            return Results.Empty;
        });
        return app;
    }
}