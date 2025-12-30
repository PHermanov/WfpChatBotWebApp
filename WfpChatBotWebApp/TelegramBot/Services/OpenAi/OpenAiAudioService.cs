using OpenAI.Audio;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiAudioService
{
    Task<string> ProcessAudio(
        Stream audioStream,
        CancellationToken cancellationToken);
}

public class OpenAiAudioService(
    IOpenAiClientFactory openAiClientFactory)
    : IOpenAiAudioService
{
    public async Task<string> ProcessAudio(
        Stream audioStream,
        CancellationToken cancellationToken)
    {
        audioStream.Position = 0;
        var audioTranscriptionOptions = new AudioTranscriptionOptions { Language = "uk" };

        var audioTranscriptionResult = await openAiClientFactory.AudioClient
            .TranscribeAudioAsync(audioStream, "voice.wav", audioTranscriptionOptions, cancellationToken);
        
        return audioTranscriptionResult.Value.Text;
    }
}