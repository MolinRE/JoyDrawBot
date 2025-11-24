using System.ComponentModel.DataAnnotations;

namespace JoyDrawBot.Configuration;

public sealed class BotOptions
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

