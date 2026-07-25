namespace Api.Domain;

public class Challenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public string? Url { get; set; }
    public Cadence Cadence { get; set; }
    public int TargetCount { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public required string Color { get; set; }
    public int SortOrder { get; set; }
}
