using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.Persistence.Models;

namespace WfpChatBotWebApp.Persistence;

public interface IGameRepository
{
    Task CheckPlayerAsync(long chatId, long userId, string userName);
    Task<List<BotUser>> GetAllPlayersAsync(long chatId);
    Task<BotUser?> GetUserByUserIdAsync(long chatId, long userId);
    Task<BotUser?> GetPlayerByUserNameAsync(long chatId, string userName);
    Task<Result?> GetTodayResultAsync(long chatId);
    Task<Result?> GetYesterdayResultAsync(long chatId);
    Task<Result?> GetLastPlayedGameAsync(long chatId);
    Task SaveResultAsync(Result result);
    Task<List<PlayerCountViewModel>> GetAllWinnersForMonthAsync(long chatId, DateTime date);
    Task<PlayerCountViewModel?> GetWinnerForMonthAsync(long chatId, DateTime date);
    Task<long[]> GetAllChatsIdsAsync();
    Task<List<PlayerCountViewModel>> GetAllWinnersAsync(long chatId);
    Task<PlayerCountViewModel?> GetYearWinnerByCountAsync(long chatId, int year);
    Task<List<PlayerCountViewModel>> GetAllWinnersForYearAsync(long chatId, int year);
}