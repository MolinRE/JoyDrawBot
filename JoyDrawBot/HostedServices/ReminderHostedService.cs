using JoyDrawBot.Configuration;
using JoyDrawBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace JoyDrawBot.HostedServices;

public sealed class ReminderHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TelegramBotClient _botClient;
    private readonly TimeZoneInfo _timeZone;
    private readonly ReminderOptions _options;
    private readonly ILogger<ReminderHostedService> _logger;

    public ReminderHostedService(
        IServiceProvider serviceProvider,
        TelegramBotClient botClient,
        IOptions<ReminderOptions> options,
        IOptions<ParsingOptions> parsingOptions,
        ILogger<ReminderHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _botClient = botClient;
        _options = options.Value;
        _timeZone = ResolveTimeZone(parsingOptions.Value);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Служба напоминаний запущена. Интервал проверки: {Interval} секунд.", _options.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Ошибка при обработке напоминаний");
            }

            await Task.Delay(_options.CheckInterval, stoppingToken);
        }
    }

    private async Task CheckForRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var contestService = scope.ServiceProvider.GetRequiredService<ContestService>();
        var utcNow = DateTimeOffset.UtcNow;
        var dueEntries = await contestService.GetDueEntriesAsync(utcNow, cancellationToken);

        foreach (var entry in dueEntries)
        {
            var message = BuildReminderMessage(entry);
            await _botClient.SendMessage(entry.UserId, message, cancellationToken: cancellationToken);
            await contestService.MarkReminderSentAsync(entry, utcNow, cancellationToken);
        }

        if (dueEntries.Count > 0)
        {
            _logger.LogInformation("Отправлено {Count} напоминаний пользователям.", dueEntries.Count);
        }
    }

    private string BuildReminderMessage(Domain.ContestEntry entry)
    {
        var lines = new List<string>
        {
            "Сегодня подводят итоги конкурса, в котором ты участвуешь.",
            $"Дата из объявления: {FormatDate(entry.ResultsAt)}.",
            "Проверь результаты и при желании отпишись от каналов:"
        };

        if (entry.Channels.Count == 0)
        {
            lines.Add("• Каналы не были указаны.");
        }
        else
        {
            lines.AddRange(entry.Channels.Select(channel => $"• {channel.Label}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static TimeZoneInfo ResolveTimeZone(ParsingOptions options)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(options.DefaultTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private string FormatDate(DateTimeOffset utcDate)
    {
        var local = TimeZoneInfo.ConvertTime(utcDate, _timeZone);
        return $"{local:dd.MM.yyyy HH:mm}";
    }
}

