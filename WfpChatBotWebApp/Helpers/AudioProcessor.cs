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
            
            audioStream.Position = 0;
            
            var output = new MemoryStream();
            
            FFMpegArguments
                .FromPipeInput(new StreamPipeSource(audioStream))
                .OutputToPipe(new StreamPipeSink(output), options => options.ForceFormat("wav"))
                .ProcessSynchronously(true, new FFOptions { BinaryFolder = "StaticFiles" });

            return output;
        }
        catch (Exception e)
        {
            logger.LogError("{Name} Exception {e}", nameof(AudioProcessor), e.Message);
            throw;
        }
    }
}