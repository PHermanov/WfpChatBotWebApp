namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public class FoundryAgentOptions
{
    public required string FoundryProjectEndpoint { get; init; }
    public required string FoundryAgentName { get; init; }
    public required string FoundryAgentVersion { get; init; }
}
