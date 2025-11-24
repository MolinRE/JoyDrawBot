using System.ComponentModel.DataAnnotations;

namespace JoyDrawBot.Configuration;

public sealed class ParsingOptions
{
    [Required]
    public string DefaultTimeZoneId { get; set; } = "Russian Standard Time";

    [Range(0, 23)]
    public int DefaultResultHour { get; set; } = 18;
}

