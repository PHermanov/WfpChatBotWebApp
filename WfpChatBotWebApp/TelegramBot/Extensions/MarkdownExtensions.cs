using System.Text.RegularExpressions;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static partial class MarkdownExtensions
{
    public static string EscapeMarkdownString(this string str) =>
        EscapeMarkdownString().Replace(str, @"\$1");
    
    [GeneratedRegex(@"([|\\*_])")]
    private static partial Regex EscapeMarkdownString();

    public const string NonCompleteMessagePostfix = "...";

    public static bool IsValidMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text) || text == NonCompleteMessagePostfix)
            return true;

        // Remove the incomplete message postfix before validation
        var contentToValidate = text.EndsWith(NonCompleteMessagePostfix)
            ? text[..^NonCompleteMessagePostfix.Length].TrimEnd()
            : text;

        if (string.IsNullOrEmpty(contentToValidate))
            return true;

        // Check for balanced markdown delimiters
        // Asterisks for bold (*text*)
        if (!AreDelimitersBalanced(contentToValidate, '*'))
            return false;

        // Underscores for italic (_text_)
        if (!AreDelimitersBalanced(contentToValidate, '_'))
            return false;

        // Backticks for inline code (`code`)
        if (!AreDelimitersBalanced(contentToValidate, '`'))
            return false;

        // Square brackets for links [text]
        if (CountOccurrences(contentToValidate, '[') != CountOccurrences(contentToValidate, ']'))
            return false;

        // Code blocks (```)
        if (CountOccurrences(contentToValidate, "```") % 2 != 0)
            return false;

        // Strikethrough (~~text~~)
        if (CountOccurrences(contentToValidate, "~~") % 2 != 0)
            return false;

        return true;
    }

    private static bool AreDelimitersBalanced(string text, char delimiter)
        => text.Count(c => c == delimiter) % 2 == 0;

    private static int CountOccurrences(string text, char character)
        => text.Count(c => c == character);

    private static int CountOccurrences(string text, string substring)
        => string.IsNullOrEmpty(substring) ? 0 : (text.Length - text.Replace(substring, string.Empty).Length) / substring.Length;
}