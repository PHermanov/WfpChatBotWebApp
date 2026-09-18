# Image editing and winner artwork

## Configuration and database setup

- FLUX creation and editing are implemented directly with `HttpClient` (`FluxEndpoint` + `FluxClient` under `TelegramBot/Services/OpenAi`) against Azure's documented BFL provider REST contract. There is no ElBruno.Text2Image (or other) SDK dependency; the app builds the same endpoint URL resolution (base URL, `.openai.azure.com` -> `.services.ai.azure.com` conversion, model-id-to-path mapping) that the former SDK provided, so existing `FoundryUrl`/`FluxModelName` configuration keeps working unchanged.
- Creation sends `model`, `prompt`, `width`, `height`, `output_format`, `num_images`. Editing sends `model`, `prompt`, `output_format`, `input_image` (raw base64, no `data:image/...;base64,` prefix, and no width/height/num_images). See [Microsoft's FLUX model documentation](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-flux). Automated tests assert this published contract directly.
- FLUX is the image-generation provider in both hosts. `IAiImageService.CreateImage` and `IAiImageEditService.EditImage` return `IAsyncEnumerable<byte[]>`; commands, winner artwork, and model tools upload image bytes directly. The legacy URL/bytes tuple and DALL·E provider fallback have been removed.
- For a new checkout, copy `LocalStart/appSettingsLocal.example.json` to `LocalStart/appSettingsLocal.json` and supply private values locally. The actual settings file is Git-ignored; the example retains its keys with empty values. Do not commit credentials or print them in logs. Rotate any credentials previously shared or committed; removing their text does not revoke them.
- Both LocalStart modes load the settings file copied beside the executable. Environment variables override JSON. The polling host also accepts command-line configuration overrides. Do not pass secrets through command-line arguments.
- The file verifier needs only `FoundryUrl`, `OpenAiKey`, and `FluxModelName`. Polling also needs the existing Telegram/database/integration configuration.
- The `Flux` HTTP client has a five-minute timeout in both hosts; native edits also bound the complete submission/polling/download operation to five minutes. Polling is restricted to the configured Azure origin, result downloads use HTTPS without the API key, and automatic redirects are disabled. Editing accepts PNG, JPEG, or WebP source bytes up to 10 MiB and requests 1024x1024 output.
- Before enabling `/redraw` and winner artwork, apply `sql/SeedTextMessages.sql` to the existing `TextMessages` table. It works with SQL Server and SQLite, inserts only missing keys, and never updates existing rows, so it is safe to re-run and will not overwrite customized templates. Apply the script yourself; the app does not run production data migrations automatically.
- LocalStart uses `local.db` relative to its working directory. Populate that database's `TextMessages` table before exercising bot previews. Ensure the database has the existing application schema/data.
- Winner templates use `{0}` for a JSON-quoted display name (lettering, not instructions) and `{1}` for the month/year or year. Preserve these placeholders when customizing prompts. Existing message templates retain their Telegram parse modes; new command error messages use HTML. Restart the host after changing cached template values, or allow the existing cache to expire.
- If the new error templates are absent, the command uses existing generic database messages. Missing winner prompt templates are logged and ultimately result in the existing text announcement rather than untemplated image generation.

## Telegram commands

- `/draw a funny golden cup` creates a new image.
- `/redraw add a golden cup and confetti; preserve the face` edits a photo. Use it as a photo caption or as a text reply to a photo, including a photo previously sent by the bot.
- Both commands request one image. All text after the command is the prompt; there is no image-count option. Empty prompts are rejected.
- `/redraw@YourBotName ...` is supported. The current attached photo takes precedence over the replied photo. Image documents, video, and animated stickers are not `/redraw` inputs.
- Recognized commands are routed before conversational AI and remain subject to per-user/chat command throttling.
- The bot sends edited copies. It never changes a user's actual Telegram profile photo.

## Model tool

`EditImage` is separate from `CreateImage`. Both use strict prompt-only Responses schemas. The application supplies current/replied image bytes to `EditImage`; the model does not supply URLs, file paths, or base64. No prior request's image is silently reused when a new request has no source. Replied images remain available even when their messages have already been added to chat history.

Keep local Responses history, encrypted reasoning, and tool results linked by `CallId`. Image tools return their image directly without an extra model turn. Missing/failed edits use database-backed messages, not raw provider errors. Static image stickers already supported in conversational requests may be edited; animated/video stickers are excluded.

## Winner behavior

1. Load the winner's current available profile photo.
2. Edit it using the monthly or yearly template, preserving the subject and adding a cup and celebration details.
3. If the avatar is absent/unavailable or editing fails, generate a funny illustration with the display name, cup, congratulations, and period.
4. If artwork is unavailable or photo delivery fails, send the text announcement. Caller cancellation stops work; it does not start fallback generation.

Winner selection and yearly ties are unchanged. Each image attempt is isolated so one failed winner image does not prevent the others. Captions retain the existing message/mention and include an authoritative month/year or year; AI-rendered lettering and exact pixel preservation are not guaranteed.

The production compositor, font asset, and direct SixLabors references have been removed, and there is no remaining ElBruno or ImageSharp dependency. The independent legacy `Tools/PictureProcessor` sample is outside the application solution and is unchanged. The `Pictures` client and shared sticker settings remain for other jobs.

## Manual verification

- For end-to-end bot checks, use development Telegram resources and run `dotnet run --project LocalStart/LocalStart.csproj`.
- Exercise `/draw`, `/redraw` as a caption, `/redraw` replying to a user photo, and `/redraw` replying to a bot-generated photo. Also check missing prompt/image and throttling behavior.
- For `/redraw`, use a localized prompt (for example, adding only a small red hat) and confirm the original subject and background remain recognizable. A successful response and a valid PNG alone do not establish that the source image was used.
- Ask the model to modify a supplied photo. Verify it selects `EditImage`; request a new picture to check `CreateImage`. Reply again to an edited photo to exercise history de-duplication.
- Winner artwork runs through `/monthlyjob` and `/yearlyjob` in LocalStart, which process every game-enabled chat in the configured database. Point LocalStart at a development database before using them. No automated tests invoke live jobs or bot APIs.

## Automated validation

Run `dotnet build WfpChatBotWebApp.slnx` and `dotnet test WfpChatBotWebApp.Tests/WfpChatBotWebApp.Tests.csproj`. The test suite uses fake image services, fake Telegram HTTP, and fake Responses streams. It requires no bot credentials, live Telegram resources, or Foundry access. Coverage includes command parsing/routing, source precedence, FLUX reference-image payloads, winner fallback/cancellation, caption preservation, tool argument validation, history replay, and image isolation between requests.
