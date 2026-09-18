using System.Globalization;
using System.Text.Json;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Services;

public enum WinnerPeriod { Month, Year }

public interface IWinnerArtworkService
{
    Task<byte[]?> CreateAsync(string userName, DateTime date, WinnerPeriod period, BinaryData? avatar, CancellationToken cancellationToken);
}

public class WinnerArtworkService(
    IAiImageService images,
    IAiImageEditService edits,
    ITextMessageService messages,
    ILogger<WinnerArtworkService> logger) : IWinnerArtworkService
{
    public async Task<byte[]?> CreateAsync(string userName, DateTime date, WinnerPeriod period, BinaryData? avatar, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (avatar is not null)
        {
            try
            {
                var prompt = await GetPrompt(userName, date, period, true, cancellationToken);
                await foreach (var bytes in edits.EditImage(prompt, avatar, cancellationToken: cancellationToken))
                {
                    if (bytes.Length == 0) break;
                    logger.LogInformation("Winner artwork created using avatar editing for {Period}", period);
                    return bytes;
                }
                throw new InvalidOperationException("Editing returned no image.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                logger.LogWarning("Winner avatar editing failed for {Period}: {ErrorType}: {Error}; generating fallback", period, e.GetType().Name, e.Message);
            }
        }
        else
        {
            logger.LogInformation("No winner avatar available for {Period}; generating artwork instead of editing", period);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prompt = await GetPrompt(userName, date, period, false, cancellationToken);
            await foreach (var bytes in images.CreateImage(prompt, cancellationToken: cancellationToken))
            {
                if (bytes.Length == 0) break;
                logger.LogInformation("Winner artwork created using generated fallback for {Period}", period);
                return bytes;
            }
            throw new InvalidOperationException("Generation returned no image bytes.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            logger.LogWarning("Winner artwork unavailable for {Period}: {ErrorType}: {Error}; using text announcement", period, e.GetType().Name, e.Message);
            return null;
        }
    }

    private async Task<string> GetPrompt(string userName, DateTime date, WinnerPeriod period, bool edit, CancellationToken cancellationToken)
    {
        var name = (period, edit) switch
        {
            (WinnerPeriod.Month, true) => Messages.MonthWinnerEditPrompt,
            (WinnerPeriod.Month, false) => Messages.MonthWinnerCreatePrompt,
            (WinnerPeriod.Year, true) => Messages.YearWinnerEditPrompt,
            (WinnerPeriod.Year, false) => Messages.YearWinnerCreatePrompt,
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        var template = await messages.GetMessageByNameAsync(name, cancellationToken);
        if (string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException($"Missing database image prompt: {name}.");
        var label = date.ToString(period == WinnerPeriod.Month ? "MMMM yyyy" : "yyyy", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.InvariantCulture, template, JsonSerializer.Serialize(userName), label);
    }
}
