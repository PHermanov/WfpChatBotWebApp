using FFMpegCore;
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
            var output = new MemoryStream();
            
            FFMpegArguments
                .FromPipeInput(new StreamPipeSource(audioStream))
                .OutputToPipe(new StreamPipeSink(output), options => 
                    options.ForceFormat("mp3"))
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