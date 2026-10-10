# WfpChatBotWebApp coding instructions

## Architecture and data flow
- This is a .NET 10 solution: `WfpChatBotWebApp/` is the ASP.NET Core production app; `LocalStart/` references it and runs the same bot logic through Telegram long polling; `WfpChatBotWebApp.Tests/` is the xUnit test project.
- Production startup is centralized in `WfpChatBotWebApp/Program.cs`: Azure Key Vault configuration, Azure Monitor, SQL Server EF Core, Telegram/HTTP clients, MediatR, and an in-memory SlimMessageBus are registered there.
- Telegram posts to `POST /telegrambot`. `TelegramBotController` validates `X-Telegram-Bot-Api-Secret-Token`, publishes the `Update` without awaiting it, and returns immediately; ten scoped consumers call `ITelegramBotService.HandleUpdateAsync`, which routes mentions/photos to AI replies, slash commands to MediatR, and ordinary text to auto-reply services.
- `JobController` maps secret-protected job names to MediatR requests. `LocalStart/LocalTelegramBotService.cs` exposes equivalent `/dailyjob`, `/monthlyjob`, etc. commands for local debugging.

## Established implementation patterns
- Model bot commands as a `CommandBase`-derived request plus an `IRequestHandler<T>` in the same file; add parsing in `TelegramBot/Commands/Common/CommandParser.cs`. Follow `TodayCommand.cs` as the representative pattern.
- Model scheduled work as request/handler pairs under `TelegramBot/Jobs/`; handlers iterate game-enabled chats and isolate per-chat failures, as in `DailyWinnerJob.cs`.
- Keep Telegram API error handling in `TelegramBot/Extensions/TelegramBotClientExtensions.cs`; prefer its `TrySend*`/`TryEdit*` helpers and pass the caller logger and cancellation token.
- Pass `CancellationToken` through controllers, handlers, EF Core calls, Telegram calls, and AI streaming. Use primary constructors and file-scoped namespaces, matching existing C# 14 code.
- Persistence is behind `IGameRepository`; all user/result queries are scoped by Telegram `chatId`. `GameRepository.CheckUserAsync` also creates missing chats and caches known users for one hour.
- Message templates, stickers, users, chats, and game results are database data (`AppDbContext`), not hard-coded response text. Preserve the existing Telegram `ParseMode` expected by each template.
- AI replies stream from `OpenAiChatService` through `BotReplyService`. Preserve Telegram thread-keyed conversations, tool-call handling, Telegram-supported HTML validation, and throttled message edits.

## Configuration and integrations
- Production loads secrets through `AzureKeyVaultUri` and `DefaultAzureCredential`; expected keys are referenced in `Program.cs` and the options classes (`FoundryAgentOptions`, `ThrottlingServiceOptions`). Do not add Foundry agent keys to `appsettings.json`; LocalStart uses `appSettingsLocal.json`, production uses Key Vault.
- `LocalStart` loads `appSettingsLocal.json`, uses SQLite `local.db`, console logging, and long polling instead of webhooks/SQL Server. Never copy credential values from local settings into code, docs, tests, or logs.
- Named HTTP clients are `Google`, `Pictures`, `Random`, and `Flux`; Telegram uses the typed `ITelegramBotClient`. Keep these names when resolving clients and register named clients in both hosts.
- External systems include Telegram Bot API, Microsoft Foundry (agent and FLUX), Google Custom Search, random.org, Azure SQL/SQLite, Azure Key Vault, Azure Monitor, and Azure Blob-hosted stickers.

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

### AI and image integration
- Use the Foundry agent for Astra chat: `AddOpenAiClients()` (both hosts) registers `AIProjectClient` with `DefaultAzureCredential` and an `AgentReference` from `FoundryProjectEndpoint`/`FoundryAgentName`/`FoundryAgentVersion`; build a per-turn `ProjectResponsesClient` via `projectClient.OpenAI.GetProjectResponsesClientForAgent(agent, conversationId)`. The agent owns model, reasoning, persona instructions, and tools; send only input items, streaming, and `truncation`. `FoundryUrl` + `OpenAiKey` serve FLUX only.
- Use FLUX (`FluxImageService`) for all image creation and editing; `IAiImageService`/`IAiImageEditService` return image bytes (`IAsyncEnumerable<byte[]>`) that callers upload to Telegram.
- Keep history in server-side Foundry conversations keyed by `TelegramThreadKey` and mapped in `IConversationStore`; send only new items. Declare Web Search and strict prompt-only `CreateImage`/`EditImage` function tools on the agent definition via REST/SDK (the portal cannot manage function tools; script in `docs/foundry-agent.md`), keep their names in sync with `OpenAiChatToolsService`, and never send `tools`/`tool_choice` per request. Append tool outputs via `CreateProjectConversationItemsAsync` (linked by `CallId`) without an extra model turn; on failure, drop the mapping and close unanswered calls. Append web citations as the HTML sources list from `WebCitationFormatter`.
- Use file-scoped OPENAI001 pragmas for the experimental Responses APIs in OpenAI 2.9.1.
- Send edit sources to FLUX as raw base64 `input_image`; keep edit source bytes request-local (resolve current, then replied, then the conversation's last image file id from `IConversationStore`), and validate against the provider contract, not SDK serialization.
- Keep winner prompts in `TextMessages`; seed scripts and setup are documented in `docs/image-editing.md`. Preserve edit-to-generate-to-text fallbacks.
- Test image/Telegram/agent behavior with fakes (fake `TokenCredential` + `HttpClientPipelineTransport`) and assert the observed wire contract; use LocalStart with development resources for explicit manual verification (checklist in `docs/foundry-agent.md`).
- Implement FLUX calls with `HttpClient` in `FluxEndpoint`/`FluxClient` against Azure's documented BFL provider REST contract. Do not treat a successful response or valid PNG alone as proof that the source image was used.

### Testing Guidelines
- Keep automated image/AI/Telegram tests isolated with fakes; never use real Telegram resources or bot APIs in automated tests. Use LocalStart for explicit manual integration checks. Preserve local settings file structure and omit credential values from shared files, code, documentation, tests, and logs.
