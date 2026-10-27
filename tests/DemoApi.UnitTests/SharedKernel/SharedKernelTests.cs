namespace DemoApi.UnitTests.SharedKernel;

public sealed class SortParserTests
{
    private static readonly string[] Allowed = ["name", "price", "createdAt"];

    [Fact]
    public void Empty_sort_uses_the_default()
    {
        SortParser.TryParse(null, Allowed, "-createdAt", out var fields, out var error).ShouldBeTrue();

        error.ShouldBeNull();
        fields.ShouldBe([new SortField("createdAt", true)]);
    }

    [Fact]
    public void Fields_are_matched_case_insensitively_and_keep_direction()
    {
        SortParser.TryParse("PRICE, -name", Allowed, "name", out var fields, out _).ShouldBeTrue();

        fields.ShouldBe([new SortField("price", false), new SortField("name", true)]);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("name,-name")]
    [InlineData("name,price,createdAt,name")]
    [InlineData(",,,")]
    [InlineData("';--")]
    public void Unknown_repeated_or_too_many_fields_fail_with_allowed_list(string sort)
    {
        SortParser.TryParse(sort, Allowed, "name", out _, out var error).ShouldBeFalse();

        error.ShouldNotBeNull();
        error.ShouldContain("name, price, createdAt");
    }

    [Fact]
    public void Three_fields_are_allowed() =>
        SortParser.TryParse("name,price,createdAt", Allowed, "name", out var fields, out _).ShouldBeTrue();
}

public sealed class PagingTests
{
    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(3, 20, 40)]
    [InlineData(2, 100, 100)]
    public void Offset_is_page_minus_one_times_size(int page, int size, int offset) =>
        new PageRequest(page, size).Offset.ShouldBe(offset);

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    public void TotalPages_is_ceiling_and_zero_when_empty(int total, int size, int pages) =>
        new PagedResponse<int>([], 1, size, total).TotalPages.ShouldBe(pages);
}

public sealed class ETagTests
{
    [Fact]
    public void Format_is_a_quoted_strong_etag() => ETag.Format(4711).ShouldBe("\"4711\"");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("*")]
    [InlineData(" * ")]
    public void Missing_or_star_counts_as_missing(string? ifMatch) => ETag.Check(ifMatch, 5).ShouldBe(ETagCheck.Missing);

    [Theory]
    [InlineData("\"5\"")]
    [InlineData("\"4\", \"5\"")]
    public void Any_listed_strong_match_passes(string ifMatch) => ETag.Check(ifMatch, 5).ShouldBe(ETagCheck.Match);

    [Theory]
    [InlineData("\"4\"")]
    [InlineData("W/\"5\"")]
    [InlineData("5")]
    public void Stale_weak_or_unquoted_values_do_not_match(string ifMatch) => ETag.Check(ifMatch, 5).ShouldBe(ETagCheck.NoMatch);
}

public sealed class ResultTests
{
    [Fact]
    public void Failed_result_has_no_value()
    {
        Result<int> result = Error.NotFound();

        result.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Success_from_value()
    {
        Result<int> result = 3;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
    }

    [Fact]
    public void Failure_requires_an_error() => Should.Throw<ArgumentNullException>(() => Result.Failure(null!));

    [Fact]
    public void Validation_error_names_the_field()
    {
        var error = Error.Validation("price", "bad");

        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe("validation-failed");
        error.ValidationErrors!["price"].ShouldBe(["bad"]);
    }

    [Fact]
    public void Precondition_errors_have_stable_codes()
    {
        Error.PreconditionRequired.Code.ShouldBe("precondition-required");
        Error.PreconditionFailed.Code.ShouldBe("precondition-failed");
        Error.InvalidToken.Type.ShouldBe(ErrorType.InvalidToken);
    }
}