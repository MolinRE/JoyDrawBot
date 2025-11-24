namespace JoyDrawBot.Models;

public sealed record ContestParseResult(
    DateTimeOffset? ResultDate,
    IReadOnlyCollection<ContestChannelRequirement> Channels,
    IReadOnlyCollection<string> Issues);

public sealed record ContestChannelRequirement(string Label, string? Url);

