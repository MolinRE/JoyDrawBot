using System.ComponentModel.DataAnnotations;

namespace JoyDrawBot.Configuration;

public sealed class ReminderOptions
{
    private const int MinimumIntervalSeconds = 30;

    [Range(MinimumIntervalSeconds, 86_400)]
    public int CheckIntervalSeconds { get; set; } = 120;

    public TimeSpan CheckInterval => TimeSpan.FromSeconds(CheckIntervalSeconds);
}

