namespace Api.Domain;

public class Completion
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? Note { get; set; }
}
