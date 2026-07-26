using FluentValidation;

namespace Api.Features.Completions;

public class CompleteChallengeRequestValidator : AbstractValidator<CompleteChallengeRequest>
{
    public CompleteChallengeRequestValidator()
    {
        RuleFor(r => r.Note)
            .MaximumLength(500)
            .When(r => !string.IsNullOrWhiteSpace(r.Note));
    }
}
