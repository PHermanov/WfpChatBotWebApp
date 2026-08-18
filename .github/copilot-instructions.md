# WfpChatBotWebApp coding instructions

## Architecture and data flow
- This is a .NET 10 solution: `WfpChatBotWebApp/` is the ASP.NET Core production app; `LocalStart/` references it and runs the same bot logic through Telegram long polling; `WfpChatBotWebApp.Tests/` is the xUnit test project.
- Production startup is centralized in `WfpChatBotWebApp/Program.cs`: Azure Key Vault configuration, Azure Monitor, SQL Server EF Core, Telegram/HTTP clients, MediatR, and an in-memory SlimMessageBus are registered there.
- Telegram posts to `POST /telegrambot`. `TelegramBotController` validates `X-Telegram-Bot-Api-Secret-Token`, publishes the `Update` without awaiting it, and returns immediately; ten scoped consumers call `ITelegramBotService.HandleUpdateAsync`.
- `TelegramBotService` is the routing hub: register/update the chat user, then route mentions/photos to AI replies, voice to transcription, slash commands to MediatR, and ordinary text to auto-reply services.
- `JobController` maps secret-protected job names to MediatR requests. `LocalStart/LocalTelegramBotService.cs` exposes equivalent `/dailyjob`, `/monthlyjob`, etc. commands for local debugging.

## Established implementation patterns
- Model bot commands as a `CommandBase`-derived request plus an `IRequestHandler<T>` in the same file; add parsing in `TelegramBot/Commands/Common/CommandParser.cs`. Follow `TodayCommand.cs` as the representative pattern.
- Model scheduled work as request/handler pairs under `TelegramBot/Jobs/`; handlers iterate game-enabled chats and isolate per-chat failures, as in `DailyWinnerJob.cs`.
- Keep Telegram API error handling in `TelegramBot/Extensions/TelegramBotClientExtensions.cs`; prefer its `TrySend*`/`TryEdit*` helpers and pass the caller logger and cancellation token.
- Pass `CancellationToken` through controllers, handlers, EF Core calls, Telegram calls, and AI streaming. Use primary constructors and file-scoped namespaces, matching existing C# 14 code.
- Persistence is behind `IGameRepository`; all user/result queries are scoped by Telegram `chatId`. `GameRepository.CheckUserAsync` also creates missing chats and caches known users for one hour.
- Message templates, stickers, users, chats, and game results are database data (`AppDbContext`), not hard-coded response text. Preserve the existing Telegram `ParseMode` expected by each template.
- AI replies stream from `OpenAiChatService` through `BotReplyService`. Preserve per-context queues, tool-call handling, Telegram-supported HTML validation, and throttled message edits.
- Audio conversion depends on copied `StaticFiles/ffmpeg` and `StaticFiles/ffprobe`; `AudioProcessor` resolves them via `BinaryFolder = "StaticFiles"`.

## Configuration and integrations
- Production loads secrets through `AzureKeyVaultUri` and `DefaultAzureCredential`; expected keys are referenced in `Program.cs` and the OpenAI options classes.
- `LocalStart` loads `appSettingsLocal.json`, uses SQLite `local.db`, console logging, and long polling instead of webhooks/SQL Server. Never copy credential values from local settings into code, docs, tests, or logs.
- Named HTTP clients are `Google`, `Pictures`, and `Random`; Telegram uses the typed `ITelegramBotClient`. Keep these names when resolving clients.
- External systems include Telegram Bot API, Azure OpenAI/Foundry, Google Custom Search, random.org, Azure SQL/SQLite, Azure Key Vault, Azure Monitor, and Azure Blob-hosted stickers.

## Build, run, and deployment
- CI restores the test project and referenced web project, builds the web project, runs `WfpChatBotWebApp.Tests`, and only then publishes the deployment artifact.
- Run local polling with `dotnet run --project LocalStart/LocalStart.csproj`; run the webhook app with `dotnet run --project WfpChatBotWebApp/WfpChatBotWebApp.csproj` when Azure credentials/configuration are available.
- Run automated tests with `dotnet test WfpChatBotWebApp.Tests/WfpChatBotWebApp.Tests.csproj`; add focused xUnit coverage for changed behavior, then validate the affected project or full solution build.
- `.github/workflows/master_wfpchatbotwebapp.yml` runs for relevant web, test, solution, and workflow changes on `master`; failed tests block publishing and Azure Web App deployment.

## Copilot Instructions

### General Guidelines
- Keep instructions concise (20–50 lines), actionable, codebase-specific, and example-driven.
- Merge existing valuable guidance rather than replacing it blindly.
- Keep `.github/copilot-instructions.md` synchronized with the repository structure.

### Code Style
- Follow established patterns and conventions in the codebase.
- Use imperative mood for instructions (e.g., "Use X" instead of "You should use X").
