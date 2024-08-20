using WfpChatBotWebApp.Persistence.Entities;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class MentionExtensions
{
    public static string GetUserMention(this User user)
        => GetUserMention(user.UserName ?? string.Empty, user.UserId);

    private static string GetUserMention(this string userName, long userId)
        => $"[{(userName.StartsWith("@") ? userName : $"@{userName}")}](tg://user?id={userId})";
    
    public static string GetUsersMention(this IEnumerable<User> users)
        => string.Join(" ", users.Select(u => GetUserMention(u.UserName ?? string.Empty, u.UserId)));
}