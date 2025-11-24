using System.Globalization;
using System.Text.RegularExpressions;
using JoyDrawBot.Configuration;
using JoyDrawBot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace JoyDrawBot.Services;

public sealed class ContestParser
{
    private static readonly Regex ChannelRegex = new(
        @"(?:(?<handle>@[a-zA-Z\d_]{4,32})|(?<url>https?:\/\/t\.me\/[^\s]+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NumericDateRegex = new(
        @"(?<day>\d{1,2})[.\/-](?<month>\d{1,2})(?:[.\/-](?<year>\d{2,4}))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MonthNameRegex = new(
        @"(?<day>\d{1,2})\s+(?<month>январ[ья]|феврал[ья]|марта?|апрел[ья]|ма[яй]|июн[ья]|июл[ья]|августа?|сентябр[ья]|октябр[ья]|ноябр[ья]|декабр[ья])(?:\s+(?<year>\d{2,4}))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TimeRegex = new(
        @"(?<hour>[01]?\d|2[0-3])[:\.](?<minute>[0-5]\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ResultKeywords =
    [
        "итог", "результат", "подвед", "выбер", "розыгрыш", "разыг"
    ];

    private static readonly Dictionary<string, int> MonthMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["января"] = 1, ["январь"] = 1,
        ["февраля"] = 2, ["февраль"] = 2,
        ["марта"] = 3, ["март"] = 3,
        ["апреля"] = 4, ["апрель"] = 4,
        ["мая"] = 5, ["май"] = 5,
        ["июня"] = 6, ["июнь"] = 6,
        ["июля"] = 7, ["июль"] = 7,
        ["августа"] = 8, ["август"] = 8,
        ["сентября"] = 9, ["сентябрь"] = 9,
        ["октября"] = 10, ["октябрь"] = 10,
        ["ноября"] = 11, ["ноябрь"] = 11,
        ["декабря"] = 12, ["декабрь"] = 12
    };

    private readonly ILogger<ContestParser> _logger;
    private readonly TimeZoneInfo _timeZone;
    private readonly int _defaultHour;

    public ContestParser(IOptions<ParsingOptions> options, ILogger<ContestParser> logger)
    {
        _logger = logger;
        var parsingOptions = options.Value;
        _defaultHour = parsingOptions.DefaultResultHour;

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(parsingOptions.DefaultTimeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            _logger.LogWarning(ex, "Указанный часовой пояс {TimeZone} не найден. Используем UTC.", parsingOptions.DefaultTimeZoneId);
            _timeZone = TimeZoneInfo.Utc;
        }
    }

    public ContestParseResult Parse(string? rawText, MessageEntity[]? entities)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new ContestParseResult(null, Array.Empty<ContestChannelRequirement>(), new[] { "Текст сообщения пустой." });
        }

        var issues = new List<string>();
        var normalized = rawText.Trim();
        var candidates = ExtractCandidateBlocks(normalized);
        DateTimeOffset? resultDate = null;

        foreach (var block in candidates)
        {
            resultDate = TryParseDate(block);
            if (resultDate.HasValue)
            {
                break;
            }
        }

        if (!resultDate.HasValue)
        {
            issues.Add("Не удалось найти дату подведения итогов. Укажите её в сообщении, например: \"итоги 25 ноября в 20:00\".");
        }

        // TODO: Скорее всего, это бесполезный метод, т.к. ссылки на канала всегда прилетают в отдельном блоке Entities
        var channels = ParseChannels(normalized);
        if (channels.Count == 0)
        {
            if (entities != null && entities.Length > 0)
            {
                var list = new List<ContestChannelRequirement>();
                foreach (var entity in entities)
                {
                    if (entity.Url != null)
                    {
                        list.Add(new(entity.Url[(entity.Url.LastIndexOf('/') + 1)..], entity.Url));
                    }
                }

                channels = list;
            }
        }
        
        if (channels.Count == 0)
        {
            issues.Add("Не найдены каналы для подписки. Укажите @username или ссылку t.me/...");
        }

        return new ContestParseResult(resultDate, channels, issues);
    }

    private static IReadOnlyCollection<string> ExtractCandidateBlocks(string text)
    {
        var blocks = new List<string>();
        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        foreach (var line in lines)
        {
            if (ResultKeywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                blocks.Add(line);
            }
        }

        if (blocks.Count == 0)
        {
            blocks.Add(text);
        }

        return blocks;
    }

    private IReadOnlyCollection<ContestChannelRequirement> ParseChannels(string text)
    {
        var requirements = new List<ContestChannelRequirement>();
        foreach (Match match in ChannelRegex.Matches(text))
        {
            var handleGroup = match.Groups["handle"];
            var urlGroup = match.Groups["url"];

            string label;
            string? url;

            if (handleGroup.Success)
            {
                label = handleGroup.Value.Trim();
                url = $"https://t.me/{label.TrimStart('@')}";
            }
            else
            {
                url = urlGroup.Value.Trim();
                label = url;
            }

            if (!requirements.Any(existing => string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase)))
            {
                requirements.Add(new ContestChannelRequirement(label, url));
            }
        }

        return requirements;
    }

    private DateTimeOffset? TryParseDate(string block)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone);
        var match = MonthNameRegex.Match(block);

        if (match.Success)
        {
            var parsed = BuildDateFromMatch(block, match, now);
            if (parsed.HasValue)
            {
                return parsed;
            }
        }

        match = NumericDateRegex.Match(block);
        if (match.Success)
        {
            var parsed = BuildNumericDate(block, match, now);
            if (parsed.HasValue)
            {
                return parsed;
            }
        }

        return null;
    }

    private DateTimeOffset? BuildDateFromMatch(string block, Match match, DateTimeOffset nowInZone)
    {
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var monthValue = match.Groups["month"].Value;

        if (!MonthMap.TryGetValue(monthValue, out var month))
        {
            return null;
        }

        var year = ParseYear(match.Groups["year"].ValueSpan, nowInZone.Year);
        return BuildDate(block, day, month, year, match);
    }

    private DateTimeOffset? BuildNumericDate(string block, Match match, DateTimeOffset nowInZone)
    {
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        var year = ParseYear(match.Groups["year"].ValueSpan, nowInZone.Year);
        return BuildDate(block, day, month, year, match);
    }

    private DateTimeOffset? BuildDate(string block, int day, int month, int year, Match match)
    {
        try
        {
            var (hour, minute) = ExtractTime(block, match);
            var localDate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
            var offset = _timeZone.GetUtcOffset(localDate);
            var result = new DateTimeOffset(localDate, offset).ToUniversalTime();

            if (result < DateTimeOffset.UtcNow)
            {
                var nextYear = new DateTime(year + 1, month, day, hour, minute, 0, DateTimeKind.Unspecified);
                var nextOffset = _timeZone.GetUtcOffset(nextYear);
                result = new DateTimeOffset(nextYear, nextOffset).ToUniversalTime();
            }

            return result;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Не удалось собрать дату из значения {Day}/{Month}/{Year}", day, month, year);
            return null;
        }
    }

    private (int hour, int minute) ExtractTime(string block, Match match)
    {
        var start = Math.Max(0, match.Index);
        var length = Math.Min(block.Length - start, match.Length + 16);
        var slice = block.Substring(start, length);
        var timeMatch = TimeRegex.Match(slice);

        if (!timeMatch.Success && match.Index > 0)
        {
            var prefixStart = Math.Max(0, match.Index - 10);
            var prefixLength = Math.Min(block.Length - prefixStart, match.Length + 26);
            slice = block.Substring(prefixStart, prefixLength);
            timeMatch = TimeRegex.Match(slice);
        }

        if (timeMatch.Success)
        {
            var hour = int.Parse(timeMatch.Groups["hour"].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(timeMatch.Groups["minute"].Value, CultureInfo.InvariantCulture);
            return (hour, minute);
        }

        return (_defaultHour, 0);
    }

    private static int ParseYear(ReadOnlySpan<char> yearSpan, int currentYear)
    {
        if (yearSpan.IsEmpty)
        {
            return currentYear;
        }

        var yearString = yearSpan.ToString();
        var parsed = int.Parse(yearString, CultureInfo.InvariantCulture);
        return yearSpan.Length == 2 ? 2000 + parsed : parsed;
    }
}

