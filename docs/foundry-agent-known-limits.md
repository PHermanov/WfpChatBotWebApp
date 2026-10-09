# Foundry agent: known limits and follow-ups

Open decisions for the Foundry agent integration (see `docs/foundry-agent.md`). None of them block the current single-instance deployment.

## Conversation retention and cleanup

- **Current:** each Telegram thread creates a Foundry conversation that is never deleted. Chat text, image inputs, and tool outputs stay in the Foundry project.
- **Risk:** unbounded storage and retention of personal chat data.
- **Option:** a `CleanupConversationsJob` request/handler under `TelegramBot/Jobs/`, exposed through `JobController` (and a LocalStart command). It would list project conversations and delete those older than N days. Conversations carry `telegram_chat_id` / `telegram_thread_key` metadata to scope or audit deletions.
- **Decide:** retention period, and whether deletion must also cover conversations whose mapping already expired in memory.

## In-memory conversation map

- **Current:** `IConversationStore` maps thread and message keys to `conv_...` IDs in `IMemoryCache` with a 7-day sliding expiry.
- **Effects:** a restart or redeploy starts new conversations for existing threads. Scaling out to more than one App Service instance gives each instance its own map, so a thread can split across conversations.
- **Option:** persist the map in the database (new entity plus SQL script applied manually) or another shared store, keeping the `IConversationStore` interface.
- **Decide:** whether conversation continuity across restarts matters, and whether scale-out is planned.

## Per-conversation lock growth

- **Current:** `OpenAiChatService` keeps one `SemaphoreSlim` per conversation in `_conversationLocks` and never removes it.
- **Effect:** slow memory growth proportional to the number of conversations over the process lifetime. It is negligible at current volume.
- **Option:** store the lock alongside the mapping with the same expiry, or remove idle locks after a turn completes.

## SDK version

- **Current:** `Azure.AI.Projects` `2.0.0-beta.2` (prerelease); conversation and response clients come from `projectClient.OpenAI`.
- **Available:** stable `2.0.1`; `3.0.0-beta.x` exposes `ProjectOpenAIClient` and `AgentAdministrationClient`.
- **Option:** upgrade to `2.0.1` as a separate change. Type or namespace names may differ; rebuild, run the wire-contract tests, and repeat the LocalStart checklist.

## Agent definition drift

- **Current:** `FoundryAgentVersion` is pinned. Function tools (`CreateImage`, `EditImage`) can only be added via REST/SDK, and a portal save may drop them.
- **Risk:** a new version without the functions breaks image tools; forgetting to bump the version keeps old instructions.
- **Option:** a LocalStart command that creates the agent version from code, using the tool schemas defined next to `OpenAiChatToolsService`, so the schemas exist in one place. Trade-off: code then owns the whole agent definition (model, instructions, Web Search).
- **Decide:** keep the portal + script workflow, or make code the source of truth for the agent definition.

## Request settings owned by the agent

- **Current:** requests send `truncation=auto`. Per-request `tools`/`tool_choice` are rejected when an agent is referenced, and other fields may be too.
- **Watch:** a `400 invalid_payload: Not allowed when agent is specified` naming a new `param` means that setting must move to the agent definition.

## Unused configuration

- The app does not read `OpenAiChatModelName` or `SystemPrompt` text messages. If they exist in `LocalStart/appSettingsLocal.json`, Key Vault, or the `TextMessages` table, they can be deleted.
