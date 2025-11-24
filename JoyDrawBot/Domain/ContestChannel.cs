namespace JoyDrawBot.Domain;

public sealed class ContestChannel
{
    public int Id { get; set; }
    public int ContestEntryId { get; set; }
    public ContestEntry ContestEntry { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
}

