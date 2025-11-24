using JoyDrawBot.Domain;
using Microsoft.EntityFrameworkCore;

namespace JoyDrawBot.Data;

public sealed class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> Users => Set<UserProfile>();
    public DbSet<ContestEntry> ContestEntries => Set<ContestEntry>();
    public DbSet<ContestChannel> ContestChannels => Set<ContestChannel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("joydraw");

        modelBuilder.Entity<UserProfile>(builder =>
        {
            builder.HasKey(u => u.TelegramId);
            builder.Property(u => u.TelegramId).ValueGeneratedNever();
            builder.Property(u => u.Username).HasMaxLength(64);
            builder.Property(u => u.FirstName).HasMaxLength(128);
            builder.Property(u => u.LastName).HasMaxLength(128);
            builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            builder.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ContestEntry>(builder =>
        {
            builder.HasIndex(e => new { e.ResultsAt, e.ReminderSentAt });
            builder.HasOne(e => e.User)
                .WithMany(u => u.ContestEntries)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(e => e.SourceChatTitle).HasMaxLength(256);
            builder.Property(e => e.SourceChatUsername).HasMaxLength(64);
            builder.Property(e => e.SourceChatType).HasMaxLength(32);
            builder.Property(e => e.OriginalText).HasColumnType("text");
        });

        modelBuilder.Entity<ContestChannel>(builder =>
        {
            builder.Property(c => c.Label).HasMaxLength(128);
            builder.Property(c => c.Url).HasMaxLength(256);

            builder.HasOne(c => c.ContestEntry)
                .WithMany(e => e.Channels)
                .HasForeignKey(c => c.ContestEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

