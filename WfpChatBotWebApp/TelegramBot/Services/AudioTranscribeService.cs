using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IAudioTranscribeService
{
    Task Reply(Message message, CancellationToken cancellationToken);
}

public class AudioTranscribeService(
    ITelegramBotClient botClient,
    IOpenAiService openAiService,
    ITextMessageService messageService,
    IGameRepository repository,
    IAudioProcessor audioProcessor,
    ILogger<AudioTranscribeService> logger)
    : IAudioTranscribeService
{
    public async Task Reply(Message message, CancellationToken cancellationToken)
    {
        try
        {
            if (message.From == null)
            {
                logger.LogError("{Name} chat: {ChatId}, message.From == null", nameof(AudioTranscribeService), message.Chat.Id);
                return;
            }

            var user = await repository.GetUserByUserIdAndChatIdAsync(message.Chat.Id, message.From.Id, cancellationToken);
            if (user == null)
            {
                logger.LogError("{Name} chat: {ChatId}, user == null", nameof(AudioTranscribeService), message.Chat.Id);
                return;
            }

            var audioFile = await botClient.GetFile(message.Voice?.FileId ?? string.Empty, cancellationToken);
            logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, audio file info loaded, Mime {mime}", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id, message.Voice?.MimeType);

            if (audioFile.FilePath == null)
            {
                logger.LogError("{Name} chat: {ChatId}, user {UserId}, FilePath == null", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id);
                return;
            }

            var audioStream = new MemoryStream();
            await botClient.DownloadFile(audioFile.FilePath, audioStream, cancellationToken);
            logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, audio file downloaded", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id);
            
            var convertedAudioStream = audioProcessor.ConvertAudio(audioStream);
            logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, audio converted to wav", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id);

            var transcript = await openAiService.ProcessAudio(convertedAudioStream, cancellationToken);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, transcript is empty", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id);
                return;
            }
            
            var template = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.AudioTranscriptTestTemplate, cancellationToken);

            var messageText = string.IsNullOrWhiteSpace(template)
                ? transcript
                : string.Format(template, user.UserName, transcript);
            
            await botClient.TrySendTextMessageAsync(
                chatId: message.Chat.Id,
                text: messageText,
                parseMode: ParseMode.Html,
                replyToMessageId: message.MessageId,
                logger: logger,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in Reply, {Name} for char {ChatId}, user {UserId}", nameof(AudioTranscribeService), message.Chat.Id, message.From?.Id);
        }
    }
}