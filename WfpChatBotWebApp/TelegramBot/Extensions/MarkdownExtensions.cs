using System.Text.RegularExpressions;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static partial class MarkdownExtensions
{
    public static string EscapeMarkdownString(this string str) =>
        EscapeMarkdownString().Replace(str, @"\$1");
    
    [GeneratedRegex(@"([|\\*_])")]
    private static partial Regex EscapeMarkdownString();
}