using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Pipes;

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
            logger.LogInformation("ConvertAudio: Audio stream received, length: {len}", audioStream.Length);
            
            var inputFileName = Path.GetTempFileName();
            logger.LogInformation("ConvertAudio: tmp file created: {filename}", inputFileName);
            
            using var fileStream = new FileStream(inputFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            audioStream.CopyTo(fileStream);

            var output = new MemoryStream();
            
            FFMpegArguments
                .FromFileInput(inputFileName, true, options => options.WithCustomArgument("-v 48"))
                .OutputToPipe(new StreamPipeSink(output), options => options.ForceFormat("mp3"))
                .ProcessSynchronously(true, new FFOptions { BinaryFolder = "StaticFiles", TemporaryFilesFolder = "/tmp" });

            return output;
        }
        catch (Exception e)
        {
            logger.LogError("{Name} Exception {e}", nameof(AudioProcessor), e.Message);
            throw;
        }
    }
}