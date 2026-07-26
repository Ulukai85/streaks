namespace Api.Features.Completions;

public record CompletionResponse(Guid Id, Guid ChallengeId, DateOnly PeriodStart, DateTimeOffset CompletedAt, string? Note);
