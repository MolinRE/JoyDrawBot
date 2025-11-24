namespace JoyDrawBot.Domain;

public sealed class ContestEntry
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public UserProfile User { get; set; } = null!;

    public long? SourceChatId { get; set; }
    public string? SourceChatTitle { get; set; }
    public string? SourceChatUsername { get; set; }
    public string? SourceChatType { get; set; }
    public int? SourceMessageId { get; set; }
    public string OriginalText { get; set; } = string.Empty;

    public DateTimeOffset ResultsAt { get; set; }
    public DateTimeOffset? ReminderSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ContestChannel> Channels { get; set; } = new List<ContestChannel>();
}

