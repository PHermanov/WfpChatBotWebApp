using WfpChatBotWebApp.Persistence.Entities;

namespace WfpChatBotWebApp.TelegramBot;

public static class MentionExtensions
{
    public static string GetUserMention(this BotUser user)
        => GetUserMention(user.UserName, user.UserId);

    private static string GetUserMention(this string userName, long userId)
        => @$"[{(userName.StartsWith("@") ? userName : $"@{userName}")}](tg://user?id={userId})";
    
}