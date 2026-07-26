namespace Api.Features.Completions;

public record CompleteChallengeRequest(DateOnly? PeriodStart, string? Note);
