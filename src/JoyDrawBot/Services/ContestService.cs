using JoyDrawBot.Data;
using JoyDrawBot.Domain;
using JoyDrawBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace JoyDrawBot.Services;

public sealed class ContestService
{
    private readonly BotDbContext _dbContext;
    private readonly ILogger<ContestService> _logger;

    public ContestService(BotDbContext dbContext, ILogger<ContestService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserProfile> UpsertUserAsync(User user, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Users.FirstOrDefaultAsync(u => u.TelegramId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile
            {
                TelegramId = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName
            };

            _dbContext.Users.Add(profile);
        }
        else
        {
            profile.Username = user.Username;
            profile.FirstName = user.FirstName;
            profile.LastName = user.LastName;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<ContestEntry> SaveContestAsync(Message message, ContestParseResult parseResult, CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            throw new InvalidOperationException("Не удалось определить автора сообщения.");
        }

        if (!parseResult.ResultDate.HasValue)
        {
            throw new InvalidOperationException("Дата подведения итогов обязательна.");
        }

        var user = await UpsertUserAsync(message.From, cancellationToken);

        var entry = new ContestEntry
        {
            UserId = user.TelegramId,
            SourceChatId = message.ForwardFromChat?.Id ?? message.ForwardFrom?.Id,
            SourceChatTitle = message.ForwardFromChat?.Title,
            SourceChatUsername = message.ForwardFromChat?.Username,
            SourceChatType = message.ForwardFromChat?.Type.ToString(),
            SourceMessageId = message.ForwardFromMessageId,
            OriginalText = ExtractMessageText(message),
            ResultsAt = parseResult.ResultDate.Value,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var channel in parseResult.Channels)
        {
            entry.Channels.Add(new ContestChannel
            {
                Label = channel.Label,
                Url = channel.Url
            });
        }

        await _dbContext.ContestEntries.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<IReadOnlyCollection<ContestEntry>> GetUpcomingEntriesAsync(long userId, CancellationToken cancellationToken)
    {
        return await _dbContext.ContestEntries
            .Include(e => e.Channels)
            .Where(e => e.UserId == userId && e.ReminderSentAt == null)
            .OrderBy(e => e.ResultsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ContestEntry>> GetDueEntriesAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        return await _dbContext.ContestEntries
            .Include(e => e.Channels)
            .Where(e => e.ResultsAt <= utcNow && e.ReminderSentAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkReminderSentAsync(ContestEntry entry, DateTimeOffset sentAt, CancellationToken cancellationToken)
    {
        entry.ReminderSentAt = sentAt;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ExtractMessageText(Message message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text;
        }

        if (!string.IsNullOrWhiteSpace(message.Caption))
        {
            return message.Caption;
        }

        return string.Empty;
    }
}

