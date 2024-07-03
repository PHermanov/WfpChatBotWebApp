using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.Persistence.Models;

namespace WfpChatBotWebApp.Persistence;

public interface IGameRepository
{
    Task CheckUserAsync(long chatId, long userId, string userName, CancellationToken cancellationToken);
    Task<List<BotUser>> GetAllUsersAsync(long chatId);
    Task<BotUser[]> GetActiveUsersAsync(long chatId, CancellationToken cancellationToken);
    Task<BotUser?> GetUserByUserIdAsync(long chatId, long userId, CancellationToken cancellationToken);
    Task<BotUser?> GetUserByNameAsync(long chatId, string userName);
    Task<Result?> GetTodayResultAsync(long chatId, CancellationToken cancellationToken);
    Task<Result?> GetYesterdayResultAsync(long chatId);
    Task<Result?> GetLastPlayedGameAsync(long chatId);
    Task SaveResultAsync(Result result, CancellationToken cancellationToken);
    Task<List<PlayerCountViewModel>> GetAllWinnersForMonthAsync(long chatId, DateTime date);
    Task<PlayerCountViewModel?> GetWinnerForMonthAsync(long chatId, DateTime date);
    Task<long[]> GetAllChatsIdsAsync(CancellationToken cancellationToken);
    Task<List<PlayerCountViewModel>> GetAllWinnersAsync(long chatId);
    Task<PlayerCountViewModel?> GetYearWinnerByCountAsync(long chatId, int year);
    Task<List<PlayerCountViewModel>> GetAllWinnersForYearAsync(long chatId, int year);
    Task<StickerEntity[]> GetStickersBySetAsync(string set, CancellationToken cancellationToken);
}