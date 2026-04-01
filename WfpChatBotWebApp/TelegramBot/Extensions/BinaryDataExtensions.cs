namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class BinaryDataExtensions
{
    public static string GetImageMediaType(this BinaryData? image)
    {
        if (image == null)
            return string.Empty;

        var bytes = image.ToArray();

        // WebP: "RIFF"...."WEBP" (bytes 0-3 == 'R','I','F','F' and bytes 8-11 == 'W','E','B','P')
        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return "image/webp";
        }

        // JPEG
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // PNG
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        // GIF
        if (bytes.Length >= 3 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
            return "image/gif";

        // BMP
        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
            return "image/bmp";

        // AVIF (ftyp...avif)
        if (bytes.Length >= 12 &&
            bytes[4] == (byte)'f' && bytes[5] == (byte)'t' && bytes[6] == (byte)'y' && bytes[7] == (byte)'p' &&
            bytes[8] == (byte)'a' && bytes[9] == (byte)'v' && bytes[10] == (byte)'i' && bytes[11] == (byte)'f')
        {
            return "image/avif";
        }

        // Fallback: preserve previous behavior to avoid breaking existing callers.
        return "image/jpeg";
    }
}
