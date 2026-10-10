namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

public sealed record ImageToolContext(
    BinaryData? SourceImage,
    string MissingImageMessage = "",
    string EditFailureMessage = "",
    Func<CancellationToken, ValueTask<BinaryData?>>? LoadThreadImage = null);
