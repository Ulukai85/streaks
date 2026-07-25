using Api.Domain;
using FluentValidation;

namespace Api.Features.Challenges;

public class CreateChallengeRequestValidator : AbstractValidator<CreateChallengeRequest>
{
    public CreateChallengeRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);

        RuleFor(r => r.Cadence)
            .Must(c => Enum.TryParse<Cadence>(c, ignoreCase: true, out _))
            .WithMessage("Cadence must be one of: Daily, Weekly, Monthly.");

        RuleFor(r => r.Color)
            .Must(c => ChallengeColors.Palette.Contains(c))
            .WithMessage($"Color must be one of: {string.Join(", ", ChallengeColors.Palette)}.");

        RuleFor(r => r.Url)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            .When(r => !string.IsNullOrWhiteSpace(r.Url))
            .WithMessage("Url must be an absolute http or https URL.");

        RuleFor(r => r.SortOrder)
            .GreaterThanOrEqualTo(0)
            .When(r => r.SortOrder.HasValue);
    }
}
