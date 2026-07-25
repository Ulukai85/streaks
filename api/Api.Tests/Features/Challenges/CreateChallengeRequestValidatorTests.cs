using Api.Features.Challenges;
using FluentValidation.TestHelper;

namespace Api.Tests.Features.Challenges;

public class CreateChallengeRequestValidatorTests
{
    private readonly CreateChallengeRequestValidator _validator = new();

    private static CreateChallengeRequest ValidRequest(
        string name = "Read every day",
        string? url = null,
        string cadence = "Daily",
        string color = "blue",
        int? sortOrder = null) => new(name, url, cadence, color, sortOrder);

    [Fact]
    public void Empty_Name_Is_Invalid()
    {
        var result = _validator.TestValidate(ValidRequest(name: ""));

        result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Theory]
    [InlineData("Daily")]
    [InlineData("Weekly")]
    [InlineData("Monthly")]
    [InlineData("daily")]
    public void Known_Cadence_Is_Valid(string cadence)
    {
        var result = _validator.TestValidate(ValidRequest(cadence: cadence));

        result.ShouldNotHaveValidationErrorFor(r => r.Cadence);
    }

    [Fact]
    public void Unknown_Cadence_Is_Invalid()
    {
        var result = _validator.TestValidate(ValidRequest(cadence: "Fortnightly"));

        result.ShouldHaveValidationErrorFor(r => r.Cadence);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("orange")]
    [InlineData("amber")]
    [InlineData("green")]
    [InlineData("teal")]
    [InlineData("blue")]
    [InlineData("indigo")]
    [InlineData("pink")]
    public void Palette_Color_Is_Valid(string color)
    {
        var result = _validator.TestValidate(ValidRequest(color: color));

        result.ShouldNotHaveValidationErrorFor(r => r.Color);
    }

    [Fact]
    public void Off_Palette_Color_Is_Invalid()
    {
        var result = _validator.TestValidate(ValidRequest(color: "chartreuse"));

        result.ShouldHaveValidationErrorFor(r => r.Color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    public void Url_Null_Empty_Or_HttpOrHttps_Is_Valid(string? url)
    {
        var result = _validator.TestValidate(ValidRequest(url: url));

        result.ShouldNotHaveValidationErrorFor(r => r.Url);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path")]
    public void Url_With_Disallowed_Scheme_Or_Relative_Is_Invalid(string url)
    {
        var result = _validator.TestValidate(ValidRequest(url: url));

        result.ShouldHaveValidationErrorFor(r => r.Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(5)]
    public void SortOrder_Null_Or_NonNegative_Is_Valid(int? sortOrder)
    {
        var result = _validator.TestValidate(ValidRequest(sortOrder: sortOrder));

        result.ShouldNotHaveValidationErrorFor(r => r.SortOrder);
    }

    [Fact]
    public void Negative_SortOrder_Is_Invalid()
    {
        var result = _validator.TestValidate(ValidRequest(sortOrder: -1));

        result.ShouldHaveValidationErrorFor(r => r.SortOrder);
    }
}
