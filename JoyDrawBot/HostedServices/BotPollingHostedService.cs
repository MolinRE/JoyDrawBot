using JoyDrawBot.Configuration;
using JoyDrawBot.Models;
using JoyDrawBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace JoyDrawBot.HostedServices;

public sealed class BotPollingHostedService : BackgroundService
{
    private static readonly ReceiverOptions ReceiverOptions = new()
    {
        AllowedUpdates = [UpdateType.Message]
    };

    private readonly TimeZoneInfo _timeZone;
    private readonly TelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotPollingHostedService> _logger;

    public BotPollingHostedService(
        TelegramBotClient botClient,
        IServiceProvider serviceProvider,
        IOptions<ParsingOptions> parsingOptions,
        ILogger<BotPollingHostedService> logger)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeZone = ResolveTimeZone(parsingOptions.Value);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, ReceiverOptions, stoppingToken);
        _logger.LogInformation("Бот Telegram запущен и готов принимать обновления.");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        if (message.Text is { } text && text.StartsWith('/'))
        {
            await HandleCommandAsync(message, text, cancellationToken);
            return;
        }

        await HandleForwardedMessageAsync(message, cancellationToken);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Ошибка при получении обновления Telegram");
        return Task.CompletedTask;
    }

    private async Task HandleCommandAsync(Message message, string command, CancellationToken cancellationToken)
    {
        var normalized = command.Split(' ')[0];
        switch (normalized)
        {
            case "/start":
            case "/help":
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Привет! Перешли мне объявление розыгрыша. Я сохраню дату итогов и список каналов, а в день результатов напомню проверить итоги и отписаться.",
                    cancellationToken: cancellationToken);
                break;
            case "/list":
                await ReplyWithUpcomingContestsAsync(message, cancellationToken);
                break;
            default:
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Неизвестная команда. Используй /help, чтобы узнать, что я умею.",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task ReplyWithUpcomingContestsAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var contestService = scope.ServiceProvider.GetRequiredService<ContestService>();
        var entries = await contestService.GetUpcomingEntriesAsync(message.From.Id, cancellationToken);

        if (entries.Count == 0)
        {
            await _botClient.SendMessage(
                message.Chat.Id,
                "У тебя пока нет сохранённых розыгрышей. Перешли объявление конкурса, чтобы я напомнил об итогах.",
                cancellationToken: cancellationToken);
            return;
        }

        var response = string.Join(
            Environment.NewLine + Environment.NewLine,
            entries.Select(entry =>
            {
                var channels = entry.Channels.Count == 0
                    ? "каналы не указаны"
                    : string.Join(", ", entry.Channels.Select(c => c.Label));

                return $"• Итоги {FormatDate(entry.ResultsAt)}: {channels}";
            }));

        await _botClient.SendMessage(
            message.Chat.Id,
            "Ближайшие розыгрыши:\n\n" + response,
            cancellationToken: cancellationToken);
    }

    private async Task HandleForwardedMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Text) && string.IsNullOrWhiteSpace(message.Caption))
        {
            await _botClient.SendMessage(
                message.Chat.Id,
                "Пришли текстовое сообщение конкурса. Я пока не умею читать изображения или голосовые сообщения.",
                cancellationToken: cancellationToken);
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var parser = scope.ServiceProvider.GetRequiredService<ContestParser>();
        var contestService = scope.ServiceProvider.GetRequiredService<ContestService>();

        var parseResult = parser.Parse(message.Text ?? message.Caption, message.Entities ?? message.CaptionEntities);
        if (!parseResult.ResultDate.HasValue)
        {
            await _botClient.SendMessage(
                message.Chat.Id,
                string.Join(Environment.NewLine, parseResult.Issues),
                cancellationToken: cancellationToken);
            return;
        }

        var entry = await contestService.SaveContestAsync(message, parseResult, cancellationToken);
        await _botClient.SendMessage(
            message.Chat.Id,
            BuildConfirmationMessage(parseResult, entry),
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    public static string SerializeDebug(object o)
    {
        return JsonSerializer.Serialize(o, new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private string BuildConfirmationMessage(ContestParseResult parseResult, Domain.ContestEntry entry)
    {
        var lines = new List<string>
        {
            $"Готово! Я напомню об итогах {FormatDate(entry.ResultsAt)}."
        };

        if (parseResult.Channels.Count > 0)
        {
            lines.Add("Следи за подписками на каналы:");
            lines.AddRange(parseResult.Channels.Select(channel => $" - {CreateLink(channel.Label, channel.Url)}"));
        }

        if (parseResult.Issues.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(parseResult.Issues.Select(issue => $"⚠️ {issue}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string CreateLink(string label, string? href)
    {
        return href == null ? label : $"<a href=\"{href}\">{label}</a>";
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

