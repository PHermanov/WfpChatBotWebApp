using NReco.VideoConverter;

namespace WfpChatBotWebApp.Helpers;

public interface IAudioProcessor
{
    MemoryStream ConvertAudio(MemoryStream audioStream);
}

public class AudioProcessor(ILogger<AudioProcessor> logger)
    : IAudioProcessor
{
    public MemoryStream ConvertAudio(MemoryStream audioStream)
    {
        try
        {
            var fileName = Path.GetTempFileName();
            using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            audioStream.CopyTo(fileStream);

            var converter = new FFMpegConverter();

            var output = new MemoryStream();
            converter.ConvertMedia(fileName, output, "mp3");

            return output;
        }
        catch (Exception e)
        {
            logger.LogError("{Name} Exception {e}", nameof(AudioProcessor), e.Message);
            throw;
        }
    }
}