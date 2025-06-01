using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.Persistence.Models;

namespace WfpChatBotWebApp.Persistence;

public interface IGameRepository
{
    Task CheckUserAsync(long chatId, long userId, string userName, CancellationToken cancellationToken);
    Task<User[]> GetActiveUsersForChatAsync(long chatId, CancellationToken cancellationToken);
    Task<User[]> GetAllUsersForChat(long chatId, CancellationToken cancellationToken);
    Task<User?> GetUserByUserIdAndChatIdAsync(long chatId, long userId, CancellationToken cancellationToken);
    Task<User?> GetUserByNameAsync(long chatId, string userName);
    Task<Result?> GetTodayResultAsync(long chatId, CancellationToken cancellationToken);
    Task<Result?> GetYesterdayResultAsync(long chatId, CancellationToken cancellationToken);
    Task<Result?> GetLastPlayedGameAsync(long chatId, CancellationToken cancellationToken);
    Task SaveResultAsync(Result result, CancellationToken cancellationToken);
    Task<PlayerCountViewModel[]> GetAllWinnersForMonthAsync(long chatId, DateTime date, CancellationToken cancellationToken);
    Task<PlayerCountViewModel?> GetWinnerForMonthAsync(long chatId, DateTime date, CancellationToken cancellationToken);
    Task<long[]> GetAllChatsIdsAsync(CancellationToken cancellationToken);
    Task<PlayerCountViewModel[]> GetAllWinnersAsync(long chatId, CancellationToken cancellationToken);
    Task<PlayerCountViewModel?> GetYearWinnerByCountAsync(long chatId, int year, CancellationToken cancellationToken);
    Task<PlayerCountViewModel[]> GetAllWinnersForYearAsync(long chatId, int year, CancellationToken cancellationToken);
    Task<Sticker[]> GetStickersBySetAsync(string set, CancellationToken cancellationToken);
    Task<Sticker?> GetImageByNameAsync(string name, CancellationToken cancellationToken);
}