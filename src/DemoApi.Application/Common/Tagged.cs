namespace DemoApi.Application.Common;

/// <summary>A response body plus the ETag header value the endpoint should send.</summary>
public sealed record Tagged<T>(T Body, string ETag);