# Image generation, editing, and winner artwork

## FLUX provider

- FLUX creates and edits all images: `/draw`, `/redraw`, the agent's `CreateImage`/`EditImage` tools, and winner artwork. `FluxImageService` implements `IAiImageService.CreateImage` and `IAiImageEditService.EditImage`; both return `IAsyncEnumerable<byte[]>`, and callers upload the bytes to Telegram.
- `FluxEndpoint` and `FluxClient` (under `TelegramBot/Services/OpenAi`) call Azure's Black Forest Labs provider REST API with `HttpClient`. See [Microsoft's FLUX model documentation](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-flux).
- Endpoint resolution from `FoundryUrl` and `FluxModelName`:
  - A base resource URL becomes `{base}/providers/blackforestlabs/v1/{model-path}?api-version=preview`; `.openai.azure.com` hosts become `.services.ai.azure.com`.
  - A URL with an `/openai/` path is rebuilt the same way from its host.
  - A full `/providers/blackforestlabs/` URL or any other custom path is used as-is.
  - Model ids map to BFL paths (`FLUX.2-pro` ? `flux-2-pro`, `FLUX.1-Kontext-pro` ? `flux-1-kontext-pro`, and so on).
- Request contract:
  - Creation sends `model`, `prompt`, `width`/`height` (1024), `output_format` (`png`), and `num_images` (1).
  - Editing sends `model`, `prompt`, `output_format`, and `input_image` as raw base64 (no `data:image/...;base64,` prefix and no width/height/num_images).
- Edit sources must be PNG, JPEG, or WebP and at most 10 MiB.
- The `Flux` HTTP client has a five-minute timeout in both hosts, and each operation (submission, polling, download) is bounded to five minutes. Polling stays on the configured Azure origin, result downloads use HTTPS without the API key, and automatic redirects are disabled.

## Configuration and database setup

- Settings: `FoundryUrl`, `OpenAiKey`, and `FluxModelName`. LocalStart reads them from `LocalStart/appSettingsLocal.json`; production reads them from Key Vault.
- For a new checkout, copy `LocalStart/appSettingsLocal.example.json` to `LocalStart/appSettingsLocal.json` and fill in private values locally. The settings file is Git-ignored. Environment variables and command-line arguments override JSON; do not pass secrets on the command line. Never commit credentials or print them in logs, and rotate any credential that was ever shared or committed.
- Apply `sql/SeedTextMessages.sql` to the `TextMessages` table before using `/redraw` and winner artwork. It works with SQL Server and SQLite, inserts only missing keys, and never updates existing rows, so it is safe to re-run. Apply it yourself; the app does not run data migrations.
- LocalStart uses `local.db` relative to its working directory; populate its `TextMessages` table before testing.
- Winner templates use `{0}` for a JSON-quoted display name (lettering, not instructions) and `{1}` for the month/year or year. Keep these placeholders when customizing prompts. Each template keeps its Telegram parse mode; command error messages use HTML. Template values are cached, so restart the host or wait for the cache to expire after changing them.
- If an image error template is missing, commands fall back to generic database messages. If a winner prompt template is missing, the job logs it and sends the text announcement instead of generating an image.

## Telegram commands

- `/draw a funny golden cup` creates a new image.
- `/redraw add a golden cup and confetti; preserve the face` edits a photo. Use it as a photo caption or as a text reply to a photo, including a photo sent by the bot.
- Both commands produce one image. All text after the command is the prompt; empty prompts are rejected.
- `/redraw@YourBotName ...` is supported. An attached photo takes precedence over a replied photo. Image documents, video, and animated stickers are not `/redraw` inputs.
- Commands are routed before conversational AI and are subject to per-user/chat command throttling.
- The bot sends edited copies and never changes a user's Telegram profile photo.

## Agent image tools

- `CreateImage` and `EditImage` are strict, prompt-only function tools declared on the Foundry agent (see `docs/foundry-agent.md`). `OpenAiChatToolsService` validates the tool name and the non-empty `prompt` argument and runs FLUX.
- The application supplies the source image bytes to `EditImage`; the model never supplies URLs, file paths, or base64. Source precedence: the current image, then the replied image, then the last image in the thread's Foundry conversation. Replied images stay available even when their messages are already part of the conversation.
- After each successful turn, `BotReplyService` records the newest image the turn added to the conversation (a user photo or static sticker, or a bot-generated photo) as a Telegram file id in `IConversationStore`. The file is downloaded only when `EditImage` runs, so image bytes stay request-local.
- Static image stickers can be edited; animated and video stickers cannot.
- Images go straight to Telegram, and the tool output is appended to the Foundry conversation (linked by `CallId`) without an extra model turn. A missing or failed edit returns a database-backed message instead of a raw provider error.

## Winner artwork

1. Load the winner's current profile photo.
2. Edit it with the monthly or yearly template, preserving the subject and adding a cup and celebration details.
3. If the avatar is unavailable or editing fails, generate an illustration with the display name, cup, congratulations, and period.
4. If artwork is unavailable or photo delivery fails, send the text announcement. Caller cancellation stops work and does not start fallback generation.

Each winner's image attempt is isolated, so one failure does not affect the others. Captions keep the winner message and mention and include the authoritative month/year or year; AI-rendered lettering and exact pixel preservation are not guaranteed. The `Pictures` HTTP client and sticker settings serve other jobs.

## Manual verification

- Use development Telegram resources and run `dotnet run --project LocalStart/LocalStart.csproj`.
- Exercise `/draw`, `/redraw` as a caption, `/redraw` replying to a user photo, and `/redraw` replying to a bot-generated photo. Also check missing prompt/image and throttling.
- For `/redraw`, use a localized prompt (for example, adding only a small red hat) and confirm the subject and background stay recognizable. A successful response and a valid PNG alone do not prove the source image was used.
- Ask the agent to modify a supplied photo and confirm it calls `EditImage`; ask for a new picture to check `CreateImage`. Reply to an edited photo to check that replied images are still found. Reply to a bot text answer in a thread that contains a photo and ask for an edit to check that the last thread image is used.
- Winner artwork runs through `/monthlyjob` and `/yearlyjob` in LocalStart, which process every game-enabled chat in the configured database; point LocalStart at a development database first.

## Automated validation

conversation reuse and tool-output appends, and the last-thread-image fallback.
