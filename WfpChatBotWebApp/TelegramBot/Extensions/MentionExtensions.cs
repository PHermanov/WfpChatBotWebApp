using WfpChatBotWebApp.Persistence.Entities;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class MentionExtensions
{
    public static string GetUserMention(this BotUser user)
        => GetUserMention(user.UserName ?? string.Empty, user.UserId);

    private static string GetUserMention(this string userName, long userId)
        => $"[{(userName.StartsWith("@") ? userName : $"@{userName}")}](tg://user?id={userId})";
    
    public static string GetUsersMention(this IEnumerable<BotUser> users)
        => string.Join(" ", users.Select(u => GetUserMention(u.UserName ?? string.Empty, u.UserId)));
}