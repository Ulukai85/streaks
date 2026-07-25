namespace Api.Features.Challenges;

public record CreateChallengeRequest(
    string Name,
    string? Url,
    string Cadence,
    string Color,
    int? SortOrder);
