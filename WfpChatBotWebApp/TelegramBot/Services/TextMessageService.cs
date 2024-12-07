using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface ITextMessageService
{
    Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken);
}

public class TextMessageService(AppDbContext appDbContext, IMemoryCache cache) : ITextMessageService
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        if (cache.TryGetValue<string>(name, out var text))
            return string.IsNullOrEmpty(text) ? string.Empty : text;

        try
        {
            await Semaphore.WaitAsync(cancellationToken);

            if (cache.TryGetValue(name, out text))
            {
                return string.IsNullOrEmpty(text) ? string.Empty : text;
            }
            else
            {
                var data = await appDbContext.TextMessages.FirstOrDefaultAsync(m => m.Name == name, cancellationToken);

                if (data != null && !string.IsNullOrEmpty(data.Text))
                {
                    cache.Set(name, data.Text, TimeSpan.FromDays(1));
                    return data.Text;
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public static class TextMessageNames
    {
        public const string WhatWanted = nameof(WhatWanted);
        public const string AllMonthWinners = nameof(AllMonthWinners);
        public const string AllWinners = nameof(AllWinners);
        public const string AllYearWinners = nameof(AllYearWinners);
        public const string Congrats = nameof(Congrats);
        public const string FuckOff = nameof(FuckOff);
        public const string Help = nameof(Help);
        public const string MainYearWinnerMultiple = nameof(MainYearWinnerMultiple);
        public const string MainYearWinnerSingle = nameof(MainYearWinnerSingle);
        public const string MamotaSays = nameof(MamotaSays);
        public const string MissedGames = nameof(MissedGames);
        public const string Month1 = "Month_1";
        public const string Month2 = "Month_2";
        public const string Month3 = "Month_3";
        public const string Month4 = "Month_4";
        public const string Month5 = "Month_5";
        public const string Month6 = "Month_6";
        public const string Month7 = "Month_7";
        public const string Month8 = "Month_8";
        public const string Month9 = "Month_9";
        public const string Month10 = "Month_10";
        public const string Month11 = "Month_11";
        public const string Month12 = "Month_12";
        public const string MonthWinner = nameof(MonthWinner);
        public const string NewWinner = nameof(NewWinner);
        public const string TodayString = nameof(TodayString);
        public const string TodayWinnerAlreadySet = nameof(TodayWinnerAlreadySet);
        public const string TodayWinnerAlreadySetUkr = nameof(TodayWinnerAlreadySetUkr);
        public const string Tomorrow = nameof(Tomorrow);
        public const string TopMonthWinners = nameof(TopMonthWinners);
        public const string WednesdayMyDudes = nameof(WednesdayMyDudes);
        public const string WinnerForever = nameof(WinnerForever);
        public const string WinnerNotSetYet = nameof(WinnerNotSetYet);
        public const string WinnerOfTheYear = nameof(WinnerOfTheYear);
        public const string YearAllMonthWinnersMsg = nameof(YearAllMonthWinnersMsg);
        public const string YearByCountWinnerMsg = nameof(YearByCountWinnerMsg);
        public const string YearSummarizeMsg = nameof(YearSummarizeMsg);
        public const string YesterdayWinner = nameof(YesterdayWinner);
        public const string UserMissing = nameof(UserMissing);
        public const string ImageDescriptionPreText = nameof(ImageDescriptionPreText);
        public const string AudioTranscriptTestTemplate = nameof(AudioTranscriptTestTemplate);
        public const string TakeRest = nameof(TakeRest);
    }
}