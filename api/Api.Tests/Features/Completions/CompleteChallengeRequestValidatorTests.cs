using Api.Features.Completions;
using FluentValidation.TestHelper;

namespace Api.Tests.Features.Completions;

public class CompleteChallengeRequestValidatorTests
{
    private readonly CompleteChallengeRequestValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Felt great today")]
    public void Null_Empty_Or_Short_Note_Is_Valid(string? note)
    {
        var result = _validator.TestValidate(new CompleteChallengeRequest(null, note));

        result.ShouldNotHaveValidationErrorFor(r => r.Note);
    }

    [Fact]
    public void Note_At_MaxLength_Is_Valid()
    {
        var note = new string('a', 500);

        var result = _validator.TestValidate(new CompleteChallengeRequest(null, note));

        result.ShouldNotHaveValidationErrorFor(r => r.Note);
    }

    [Fact]
    public void Note_Over_MaxLength_Is_Invalid()
    {
        var note = new string('a', 501);

        var result = _validator.TestValidate(new CompleteChallengeRequest(null, note));

        result.ShouldHaveValidationErrorFor(r => r.Note);
    }
}
