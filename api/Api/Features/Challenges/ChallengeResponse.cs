namespace Api.Features.Challenges;

public record ChallengeResponse(
    Guid Id,
    string Name,
    string? Url,
    string Cadence,
    int TargetCount,
    DateOnly StartsOn,
    DateTimeOffset? ArchivedAt,
    string Color,
    int SortOrder);
