namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public class OpenAiClientFactoryOptions
{
    public required string OpenAiKey { get; init; }
    public required string OpenAiUrl { get; init; }
    public required string OpenAiChatModelName { get; init; }
    public required string OpenAiImageModelName { get; init; }
    public required string OpenAiAudioModelName { get; init; }
}
