namespace WfpChatBotWebApp.TelegramBot.Services.Instagram;

public enum InstagramMediaKind
{
    Photo,
    Video
}

public sealed record InstagramMedia(InstagramMediaKind Kind, Uri Url);
