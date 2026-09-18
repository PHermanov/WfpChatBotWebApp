namespace WfpChatBotWebApp.Helpers;

public static class ImageInput
{
    public const int MaxBytes = 10 * 1024 * 1024;

    public static string? GetMediaType(BinaryData image)
    {
        var bytes = image.ToMemory().Span;
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return "image/jpeg";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        if (bytes.StartsWith("GIF"u8))
            return "image/gif";
        if (bytes.StartsWith("BM"u8))
            return "image/bmp";
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8) && bytes.Slice(8, 4).SequenceEqual("avif"u8))
            return "image/avif";
        return null;
    }

    public static string ToReferenceImage(BinaryData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.ToMemory().Length is 0 or > MaxBytes)
            throw new ArgumentException("The source image must be between 1 byte and 10 MiB.", nameof(image));

        var mediaType = GetMediaType(image);
        if (mediaType is not ("image/png" or "image/jpeg" or "image/webp"))
            throw new ArgumentException("Image editing requires PNG, JPEG, or WebP bytes.", nameof(image));

        return Convert.ToBase64String(image.ToMemory().Span);
    }
}
