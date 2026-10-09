# Foundry Agent chat integration

Astra's conversational replies run through a Microsoft Foundry agent. Image creation and editing use FLUX with `FoundryUrl`, `OpenAiKey`, and `FluxModelName` (see `docs/image-editing.md`).

## Configuration

| Key | Example | Purpose |
| --- | --- | --- |
| `FoundryProjectEndpoint` | `https://<resource>.services.ai.azure.com/api/projects/<project>` | Foundry project endpoint |
| `FoundryAgentName` | `wfp-agent` | Agent name |
| `FoundryAgentVersion` | `7` | Pinned agent version |

- LocalStart: set the keys in `LocalStart/appSettingsLocal.json` (see `appSettingsLocal.example.json` for the structure). Do not commit credentials.
- Production: add the same key names to Key Vault; `WfpChatBotWebApp/appsettings.json` intentionally has no Foundry agent keys. The web app starts without them; AI replies then fail with the fallback message and a `... must be configured` error in the log.
- The agent defines the model, reasoning settings, and persona; the app does not send them.

## Authentication

The project endpoint uses Microsoft Entra ID through `DefaultAzureCredential` (no API key).

- Locally: sign in with `az login` or a Visual Studio Azure account that has access to the Foundry project.
- Production: the App Service managed identity is used (see [App Service managed identity](#app-service-managed-identity)).
- Both identities need the **Foundry User** role (formerly Azure AI User) on the Foundry project. Grant it yourself; the app never changes Azure resources.

### App Service managed identity

A managed identity is an Entra ID identity that Azure creates for the App Service and manages for you (no secret to store or rotate). In production, `DefaultAzureCredential` signs in as this identity for both Key Vault and the Foundry project. A missing role gives `401`/`403` from the Foundry project even when LocalStart (your own account) works.

Portal:

1. App Service → **Settings → Identity** → **System assigned**: confirm **Status** is **On** and note the **Object (principal) ID**.
2. Foundry project (in Foundry: **Manage → Project details → Open in Azure portal**) → **Access control (IAM)** → **Add → Add role assignment**.
3. **Role**: **Foundry User** (may still show as Azure AI User) → **Members**: **Managed identity** → **Select members** → type **App Service** → select the web app → **Review + assign**.
4. Verify: project → **Access control (IAM)** → **Check access** → search the web app name. Role propagation can take a few minutes; restart the App Service afterwards so it fetches a fresh token.

Azure CLI (PowerShell), run yourself after `az login`:

```powershell
$rg       = "<app-service-resource-group>"
$app      = "<app-service-name>"
$aiRg     = "<foundry-resource-group>"
$account  = "<foundry-resource-name>"
$project  = "<foundry-project-name>"

# Principal ID of the system-assigned identity (empty means the identity is off; enable it with: az webapp identity assign -g $rg -n $app)
$principalId = az webapp identity show -g $rg -n $app --query principalId -o tsv

# Project resource ID used as the role scope
$scope = az resource show -g $aiRg -n "$account/$project" --resource-type "Microsoft.CognitiveServices/accounts/projects" --query id -o tsv

# Foundry User role ID (stable across the role rename)
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role "53ca6127-db72-4b80-b1b0-d745d6d5456d" --scope $scope

# Verify
az role assignment list --assignee $principalId --scope $scope --include-inherited -o table
```

Use the same commands with your user object ID (`az ad signed-in-user show --query id -o tsv`) and `--assignee-principal-type User` if your own account lacks the role.

## Agent setup

1. Put the persona in the agent instructions (see [Example agent instructions](#example-agent-instructions)). The app sends date, participants, and bot identity as conversation context.
2. Add formatting rules: reply in Telegram-supported HTML only and do not put links inline (sources are appended by the app).
3. Keep the Web Search tool on the agent and save a version.
4. Add the `CreateImage` and `EditImage` function tools with the script below, then set `FoundryAgentVersion` to the version it prints.

### Example agent instructions

A starting point for the persona. The app supplies time and participants: each conversation starts with a context message containing the UTC timestamp, participants, and bot identity, and every user message header carries its send time.

```text
You are a witty and friendly assistant living in a Telegram group chat.
Reply in the language of the message you are answering. Keep answers short, helpful and conversational.
Each user message starts with a header "Telegram UserId: <id>; Time: <UTC time>"; use it to tell participants apart and to know the current time. Do not repeat the header.
Format text only with Telegram supported HTML tags: <b>, <i>, <u>, <s>, <code>, <pre> and <a>. Never use Markdown and never use any other HTML tag.
Do not put links inline; web sources are appended to your reply automatically.
Use CreateImage when a request needs a new image and EditImage when the user wants to change an attached or replied-to image.
```

### Function tools

All tools live on the agent definition. When a request references an agent, the service rejects per-request `tools` (`400 invalid_payload: Not allowed when agent is specified`), so the app sends neither `tools` nor `tool_choice`. The Foundry portal cannot add, remove, or update function tools, and the playground does not execute function calls, so declare them through the REST API and test image tools in LocalStart.

The script reads an existing agent version, keeps its non-function tools (Web Search), adds both functions, and creates a new version. Run it yourself after `az login`; it prints no credentials.

```powershell
$endpoint = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$agent    = "<agent-name>"
$version  = "<current version number>"
$token    = az account get-access-token --scope "https://ai.azure.com/.default" --query accessToken -o tsv
$headers  = @{ Authorization = "Bearer $token" }

$current    = Invoke-RestMethod -Method Get -Uri "$endpoint/agents/$agent/versions/$version`?api-version=v1" -Headers $headers
$definition = $current.definition

$promptSchema = { param($d) @{ type = "object"; properties = @{ prompt = @{ type = "string"; description = $d } }; required = @("prompt"); additionalProperties = $false } }
$tools  = @($definition.tools | Where-Object { $_ -and $_.type -ne "function" })
$tools += @{ type = "function"; name = "CreateImage"; strict = $true
             description = "Creates an image by provided prompt."
             parameters  = & $promptSchema "The visual description of a new image to generate. Use EditImage instead when modifying a supplied image." }
$tools += @{ type = "function"; name = "EditImage"; strict = $true
             description = "Edits the current attached image or the image being replied to. Requires a supplied source image; preserves the subject while applying the requested changes. Source bytes are supplied by the application, not tool arguments."
             parameters  = & $promptSchema "Describe the changes to the supplied image and what should be preserved." }
$definition | Add-Member -NotePropertyName tools -NotePropertyValue $tools -Force

$body    = @{ definition = $definition } | ConvertTo-Json -Depth 50
$created = Invoke-RestMethod -Method Post -Uri "$endpoint/agents/$agent/versions?api-version=v1" -Headers $headers -ContentType "application/json" -Body $body
"New version: $($created.version)"
```

- The function names must match `OpenAiChatToolsService` (`CreateImage`, `EditImage`); it validates the name and the non-empty `prompt` argument of every call.
- After saving the agent in the portal, check the agent YAML still lists both functions; if a portal save dropped them, run the script again from the new version and update `FoundryAgentVersion`.

## Conversation model

- Each Telegram thread maps to a server-side Foundry conversation, which holds the history. The app sends only the new messages per turn with `truncation=auto`; tools come from the agent definition.
- Thread key `{chatId}_{id}`: `MessageThreadId` when Telegram provides it (supergroup reply threads and forum topics; a whole forum topic shares one conversation), otherwise the message itself for a new mention, or the replied message for a reply.
- Foundry assigns conversation IDs (`conv_...`), so the app keeps an in-memory map (`IConversationStore`, 7-day sliding expiry). After each successful turn, the user message and the bot answer are also mapped, so replies continue a chain in private chats and basic groups. A restart clears the map and the next message starts a new conversation.
- New conversations get a context message (UTC timestamp, participants, bot identity) and metadata `telegram_chat_id` and `telegram_thread_key`. Each user message header includes its send time.
- Turns in the same conversation run one at a time.
- Image tools send bytes straight to Telegram, then append a `function_call_output` to the conversation without an extra model turn.
- On a failed, incomplete, cancelled, or empty turn, or a tool failure, the thread mapping is removed and unanswered function calls are closed with a failure output (best effort).
- Web search `url_citation` annotations are appended to the final reply as up to five de-duplicated, HTML-encoded `🔗` links.

Chat text and images are stored in Foundry conversations and are kept until deleted; see `docs/foundry-agent-known-limits.md` for retention and other open decisions.

## LocalStart manual checklist

Run `dotnet run --project LocalStart/LocalStart.csproj` against development Telegram and Foundry resources. LocalStart logs the `WfpChatBotWebApp.TelegramBot.Services.OpenAi` namespace at Debug (stream update types, conversation/response IDs, tool calls, token usage; never message text).

1. The first AI reply logs `Using Foundry agent <name> v<version>` and `Created Foundry conversation`.
2. A plain mention streams valid HTML.
3. A current-events question logs web search updates and ends with a sources list.
4. Follow-ups continue the same conversation in a supergroup reply thread (confirm `MessageThreadId` is present), a private-chat reply chain, and a forum topic.
5. "Draw ..." calls `CreateImage`, sends the image, and makes no extra model turn; the next reply in the thread succeeds (the tool output was appended).
6. Replying to a photo with a small edit calls `EditImage` and preserves the subject.
7. Web search and image generation work in the same thread (both come from the agent definition).
8. Describing a photo works.
9. "Who is here?" uses the participant context.
10. Two quick messages in one thread are answered one after another.
11. After restarting LocalStart, replying in an old thread starts a new conversation.
12. A wrong `FoundryAgentVersion` produces the fallback message and an error log.
13. The conversation is visible in the Foundry portal with its metadata.

If a request field is rejected with `Not allowed when agent is specified`, move that setting to the agent definition instead of working around it in code. If conversation item appends are rejected, stop and investigate before changing the tool-output flow.

## Automated validation

`dotnet test WfpChatBotWebApp.Tests/WfpChatBotWebApp.Tests.csproj` uses a fake credential and fake HTTP transport and needs no Foundry or Telegram access. It asserts the SDK wire contract: `POST {project}/openai/v1/responses` with `agent_reference {type, name, version}` and `conversation {id}` and without `tools`/`tool_choice`, conversation creation with metadata and context, and `conversations/{id}/items` tool outputs.
