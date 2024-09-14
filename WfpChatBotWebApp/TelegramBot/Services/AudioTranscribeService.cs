using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;

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
    ILogger<AudioTranscribeService> logger)
    : IAudioTranscribeService
{
    public async Task Reply(Message message, CancellationToken cancellationToken)
    {
        try
        {
            if (message.From == null)
            {
                logger.LogInformation("{Name} chat: {ChatId}, message.From == null", nameof(AudioTranscribeService), message.Chat.Id);
                return;
            }

            var user = await repository.GetUserByUserIdAndChatIdAsync(message.Chat.Id, message.From.Id, cancellationToken);
            if (user == null)
            {
                logger.LogInformation("{Name} chat: {ChatId}, user == null", nameof(AudioTranscribeService), message.Chat.Id);
                return;
            }

            var audioFile = await botClient.GetFileAsync(message.Voice?.FileId ?? string.Empty, cancellationToken);
            logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, audio file info loaded", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id);

            if (audioFile.FilePath == null)
            {
                logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, FilePath == null", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id);
                return;
            }

            var audioStream = new MemoryStream();
            await botClient.DownloadFileAsync(audioFile.FilePath, audioStream, cancellationToken);
            logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, audio file downloaded", nameof(AudioTranscribeService), message.Chat.Id, message.From.Id);

            var fileName = Path.GetFileName(audioFile.FilePath);
            var transcript = await openAiService.ProcessAudio(audioStream, fileName, cancellationToken);
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
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError("{Name} for char {ChatId}, user {UserId} Exception in Reply {e}", nameof(AudioTranscribeService), message.Chat.Id, message.From?.Id, e.Message);
        }
    }
}