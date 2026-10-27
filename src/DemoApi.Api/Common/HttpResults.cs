namespace DemoApi.Api.Common;

/// <summary>The one place that maps <see cref="Result"/> to HTTP (solution-structure §6).</summary>
internal static class HttpResults
{
    public const string ProductCacheControl = "private, no-cache";

    public static IResult ToNoContent(this Result result, HttpContext context) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem(context);

    public static IResult ToOk<T>(this Result<T> result, HttpContext context) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem(context);

    public static IResult ToAccepted(this Result<AcceptedResponse> result, HttpContext context) =>
        result.IsSuccess ? TypedResults.Accepted((string?)null, result.Value) : result.Error!.ToProblem(context);

    public static IResult ToOk<T>(this Result<Tagged<T>> result, HttpContext context)
    {
        if (result.IsFailure)
        {
            return result.Error!.ToProblem(context);
        }

        context.Response.Headers.ETag = result.Value.ETag;
        return TypedResults.Ok(result.Value.Body);
    }

    public static IResult ToCreated<T>(this Result<Tagged<T>> result, HttpContext context, Func<T, string> location)
    {
        if (result.IsFailure)
        {
            return result.Error!.ToProblem(context);
        }

        context.Response.Headers.ETag = result.Value.ETag;
        return TypedResults.Created(location(result.Value.Body), result.Value.Body);
    }

    // Without this exact two-argument overload, ToOk(HttpContext) binds to the generic ToOk<T> above
    // (no optional parameter needed) and drops the ETag, sending the ProductWithETag wrapper as the body.
    public static IResult ToOk(this Result<ProductWithETag> result, HttpContext context) => result.ToOk(context, null);

    public static IResult ToOk(this Result<ProductWithETag> result, HttpContext context, string? cacheControl)
    {
        if (result.IsFailure)
        {
            return result.Error!.ToProblem(context);
        }

        context.Response.Headers.ETag = result.Value.ETag;
        if (cacheControl is not null)
        {
            context.Response.Headers.CacheControl = cacheControl;
        }

        return TypedResults.Ok(result.Value.Product);
    }

    public static IResult ToCreated(this Result<ProductWithETag> result, HttpContext context)
    {
        if (result.IsFailure)
        {
            return result.Error!.ToProblem(context);
        }

        context.Response.Headers.ETag = result.Value.ETag;
        return TypedResults.Created($"/api/v1/products/{result.Value.Product.Id}", result.Value.Product);
    }
}