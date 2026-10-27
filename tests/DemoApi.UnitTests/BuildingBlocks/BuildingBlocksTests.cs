namespace DemoApi.UnitTests.BuildingBlocks;

public sealed class IsoDatesTests
{
    [Theory]
    [InlineData("2026-10-24T09:15:00Z", "2026-10-24T09:15:00+00:00")]
    [InlineData("2026-10-24T11:15:00+02:00", "2026-10-24T09:15:00+00:00")]
    [InlineData("2026-10-24T04:15:00.123-05:00", "2026-10-24T09:15:00.123+00:00")]
    public void Dates_with_an_offset_are_converted_to_utc(string text, string expected)
    {
        IsoDates.TryParse(text, out var value).ShouldBeTrue();

        value.ShouldBe(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
        value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-10-24")]
    [InlineData("2026-10-24T09:15:00")]
    [InlineData("2026-10-24T09:15:00.0000000")]
    [InlineData("not a date at all, no")]
    [InlineData("2026-13-40T09:15:00Z")]
    public void Dates_without_an_offset_or_invalid_are_rejected(string? text) =>
        IsoDates.TryParse(text, out _).ShouldBeFalse();

    [Fact]
    public void Converter_writes_utc_with_z_and_rejects_input_without_offset()
    {
        var options = new JsonSerializerOptions { Converters = { new UtcDateTimeOffsetConverter() } };

        JsonSerializer.Serialize(new DateTimeOffset(2026, 10, 24, 11, 15, 0, TimeSpan.FromHours(2)), options).ShouldBe("\"2026-10-24T09:15:00Z\"");
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset>("\"2026-10-24T09:15:00\"", options));
    }
}

public sealed class JwtOptionsTests
{
    [Fact]
    public void Key_shorter_than_256_bits_fails_validation()
    {
        var options = new JwtOptions { SigningKey = Convert.ToBase64String(new byte[31]) };

        options.Validate(new ValidationContext(options)).ShouldContain(r => r.MemberNames.Contains(nameof(JwtOptions.SigningKey)));
    }

    [Fact]
    public void Non_base64_key_fails_validation()
    {
        var options = new JwtOptions { SigningKey = "not base64!!" };

        options.Validate(new ValidationContext(options)).ShouldNotBeEmpty();
    }

    [Fact]
    public void Previous_key_is_added_to_validation_keys()
    {
        var options = new JwtOptions
        {
            SigningKey = Convert.ToBase64String(new byte[32]),
            PreviousSigningKey = Convert.ToBase64String(new byte[48]),
            PreviousKeyId = "k0",
        };

        options.Validate(new ValidationContext(options)).ShouldBeEmpty();
        options.ValidationKeys().Select(k => k.KeyId).ShouldBe(["k1", "k0"]);
    }
}

public sealed class PostgresCircuitBreakerTests
{
    [Fact]
    public void Opens_after_threshold_and_closes_after_break_duration()
    {
        var time = new FakeTimeProvider();
        var breaker = new PostgresCircuitBreaker(time) { FailureThreshold = 3, BreakDuration = TimeSpan.FromSeconds(15) };

        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.IsOpen.ShouldBeFalse();
        breaker.RecordFailure();
        breaker.IsOpen.ShouldBeTrue();

        time.Advance(TimeSpan.FromSeconds(15));
        breaker.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public void Success_resets_the_failure_count()
    {
        var breaker = new PostgresCircuitBreaker(new FakeTimeProvider()) { FailureThreshold = 2 };

        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();

        breaker.IsOpen.ShouldBeFalse();
    }
}

public sealed class QueryHelpersTests
{
    [Theory]
    [InlineData("abc", "%abc%")]
    [InlineData(" 50% ", "%50\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("a\\b", "%a\\\\b%")]
    public void ContainsPattern_escapes_like_wildcards(string search, string expected) =>
        QueryHelpers.ContainsPattern(search).ShouldBe(expected);

    [Fact]
    public void CheckIfMatch_maps_to_428_412_or_null()
    {
        QueryHelpers.CheckIfMatch(null, 1).ShouldBe(Error.PreconditionRequired);
        QueryHelpers.CheckIfMatch("\"2\"", 1).ShouldBe(Error.PreconditionFailed);
        QueryHelpers.CheckIfMatch("\"1\"", 1).ShouldBeNull();
    }
}

public sealed class ProductCacheTests
{
    [Fact]
    public void Keys_follow_adr_0009()
    {
        var id = Guid.Parse("0196a000-0000-7000-8000-000000000001");

        ProductCache.ItemKey(true, id).ShouldBe("products:v1:item:admin:0196a000000070008000000000000001");
        ProductCache.ItemKey(false, id).ShouldStartWith("products:v1:item:customer:");
        ProductCache.ListKey(false, "abc", "q").ShouldStartWith("products:v1:list:customer:gabc:");
    }

    [Fact]
    public void List_keys_differ_by_query_and_scope()
    {
        ProductCache.ListKey(false, "g", "a").ShouldNotBe(ProductCache.ListKey(false, "g", "b"));
        ProductCache.ListKey(true, "g", "a").ShouldNotBe(ProductCache.ListKey(false, "g", "a"));
    }
}