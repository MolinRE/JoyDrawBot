# JoyDrawBot

Бот-наставник для розыгрышей в Telegram. Пользователи пересылают объявления конкурсов, бот вытаскивает дату подведения итогов и каналы, которые требуют подписки, а затем напоминает о необходимости проверить результаты и отписаться.

## Стек

- .NET 9 / Generic Host
- Telegram.Bot 22 (long polling)
- Entity Framework Core 9 + PostgreSQL (Npgsql)

## Быстрый старт

1. Создайте базу данных в PostgreSQL и пропишите строку подключения в `appsettings.json` или переменной окружения:

   ```json
   {
     "ConnectionStrings": {
       "Database": "Host=localhost;Port=5432;Database=joydraw;Username=postgres;Password=postgres"
     }
   }
   ```

2. Получите токен бота у [BotFather](https://t.me/BotFather) и пропишите его в `Bot:Token`.

3. Примените миграции (создаются автоматически при запуске) и стартуйте приложение:

   ```bash
   dotnet run --project JoyDrawBot
   ```

4. Откройте чат с ботом, отправьте `/start`, далее просто пересылайте объявления конкурсов.

## Как это работает

- `ContestParser` вытягивает дату и каналы из текста (распознаёт даты с цифрами и русскими месяцами, ссылки и `@username`).
- `ContestService` записывает всё в Postgres и позволяет запрашивать отложенные розыгрыши.
- `BotPollingHostedService` обрабатывает команды и пересланные сообщения.
- `ReminderHostedService` периодически проверяет БД и отправляет напоминания в день итогов.

## Настройки

Все ключи живут в `appsettings.json` / переменных окружения:

- `Bot:Token` — токен Telegram-бота.
- `ConnectionStrings:Database` — строка подключения к Postgres.
- `Reminder:CheckIntervalSeconds` — как часто проверять БД (по умолчанию 120 сек).
- `Parsing:DefaultTimeZoneId` — часовой пояс объявлений (по умолчанию `Russian Standard Time`).
- `Parsing:DefaultResultHour` — час, который используется, если точное время итогов не найдено.

## TODO / Идеи развития

- Обработка вложений и ссылок на посты вместо текста.
- Напоминания за несколько часов до итогов.
- Подтверждение, что пользователь действительно отписался от каналов.