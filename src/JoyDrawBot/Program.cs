using JoyDrawBot.Configuration;
using JoyDrawBot.Data;
using JoyDrawBot.HostedServices;
using JoyDrawBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection("Bot"))
    .PostConfigure(s => s.Token = builder.Configuration["JOYDRAWBOT_TOKEN"] ?? string.Empty)
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<ReminderOptions>()
    .Bind(builder.Configuration.GetSection("Reminder"))
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<ParsingOptions>()
    .Bind(builder.Configuration.GetSection("Parsing"))
    .ValidateDataAnnotations();

var connectionString = builder.Configuration.GetConnectionString("JoyDrawDb");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Не удалось найти строку подключения. Укажите ConnectionStrings:JoyDrawDb в appsettings.json или переменной окружения.");
}

builder.Services.AddDbContext<BotDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseSnakeCaseNamingConvention();
});

builder.Services.AddSingleton(sp =>
{
    var token = sp.GetRequiredService<IOptions<BotOptions>>().Value.Token;

    if (string.IsNullOrWhiteSpace(token))
    {
        throw new InvalidOperationException("В настройках отсутствует JOYDRAWBOT_TOKEN.");
    }

    return new TelegramBotClient(token);
});

builder.Services.AddScoped<ContestService>();
builder.Services.AddSingleton<ContestParser>();
builder.Services.AddHostedService<BotPollingHostedService>();
builder.Services.AddHostedService<ReminderHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();