using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.Persistence.Models;

namespace WfpChatBotWebApp.Persistence;
public class GameRepository(AppDbContext context, IMemoryCache cache) : IGameRepository
{
    #region Users 
    public async Task CheckUserAsync(long chatId, long userId, string userName, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue((chatId, userId), out bool saved) && saved)
        {
            return;
        }

        var userById = await GetUserByUserIdAndChatIdAsync(chatId, userId, cancellationToken);

        if (userById != null)
        {
            cache.Set((chatId, userId), true, TimeSpan.FromDays(30));

            if (userById.Inactive)
            {
                userById.Inactive = false;
                userById.UserName = userName;
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            var newUser = new BotUser
            {
                ChatId = chatId,
                UserId = userId,
                UserName = userName
            };

            await context.BotUsers.AddAsync(newUser, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            cache.Set((chatId, userId), true, TimeSpan.FromDays(30));
        }
    }

    public async Task<long[]> GetAllChatsIdsAsync(CancellationToken cancellationToken)
        => await context.BotUsers
            .Where(p => p.ChatId < 0) // chats ids are negative
            .Select(p => p.ChatId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    
    public async Task<BotUser[]> GetActiveUsersAsync(long chatId, CancellationToken cancellationToken)
        => await context.BotUsers.Where(p => p.ChatId == chatId && p.Inactive == false).ToArrayAsync(cancellationToken);
    
    public async Task<BotUser[]> GetInactiveUsersAsync(long chatId, CancellationToken cancellationToken)
        => await context.BotUsers.Where(p => p.ChatId == chatId && p.Inactive == true).ToArrayAsync(cancellationToken);

    public async Task<BotUser[]> GetAllUsersForChat(long chatId, CancellationToken cancellationToken)
        => await context.BotUsers.Where(p => p.ChatId == chatId).ToArrayAsync(cancellationToken);
    
    public async Task<BotUser?> GetUserByUserIdAndChatIdAsync(long chatId, long userId, CancellationToken cancellationToken)
        => await context.BotUsers.FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId, cancellationToken);

    public async Task<BotUser?> GetUserByNameAsync(long chatId, string userName)
        => await context.BotUsers.FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserName == userName);

    #endregion

    #region Results
    
    public async Task<Result?> GetTodayResultAsync(long chatId, CancellationToken cancellationToken)
        => await context.Results.FirstOrDefaultAsync(r => r.ChatId == chatId && r.PlayedAt.Date == DateTime.Today, cancellationToken);

    public async Task<Result?> GetYesterdayResultAsync(long chatId, CancellationToken cancellationToken)
        => await context.Results.FirstOrDefaultAsync(r => r.ChatId == chatId && r.PlayedAt.Date == DateTime.Today.AddDays(-1), cancellationToken);

    public async Task<Result?> GetLastPlayedGameAsync(long chatId, CancellationToken cancellationToken)
        => await context.Results
            .Where(r => r.ChatId == chatId)
            .OrderByDescending(r => r.PlayedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveResultAsync(Result result, CancellationToken cancellationToken)
    {
        await context.Results.AddAsync(result, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlayerCountViewModel[]> GetAllWinnersForMonthAsync(long chatId, DateTime date, CancellationToken cancellationToken)
            => await GetMonthResults(chatId, date).ToArrayAsync(cancellationToken);

    public async Task<PlayerCountViewModel?> GetWinnerForMonthAsync(long chatId, DateTime date, CancellationToken cancellationToken)
        => await GetMonthResults(chatId, date).FirstOrDefaultAsync(cancellationToken);

    public async Task<PlayerCountViewModel[]> GetAllWinnersAsync(long chatId, CancellationToken cancellationToken)
        => await context.Results.Where(r => r.ChatId == chatId)
            .ApplyGrouping()
            .ToArrayAsync(cancellationToken);

    private IOrderedQueryable<PlayerCountViewModel> GetMonthResults(long chatId, DateTime date)
        => context.Results
            .Where(r => r.ChatId == chatId && r.PlayedAt.Date.Year == date.Year && r.PlayedAt.Date.Month == date.Month)
            .ApplyGrouping();

    public async Task<PlayerCountViewModel?> GetYearWinnerByCountAsync(long chatId, int year)
        => await context.Results
            .Where(r => r.ChatId == chatId && r.PlayedAt.Date.Year == year)
            .ApplyGrouping()
            .FirstOrDefaultAsync();

    public async Task<PlayerCountViewModel[]> GetAllWinnersForYearAsync(long chatId, int year, CancellationToken cancellationToken)
        => await context.Results
            .Where(r => r.ChatId == chatId && r.PlayedAt.Date.Year == year)
            .ApplyGrouping()
            .ToArrayAsync(cancellationToken);
    #endregion

    #region Stickers

    public async Task<StickerEntity[]> GetStickersBySetAsync(string set, CancellationToken cancellationToken)
        => await context.Stickers
            .Where(s => s.Set == set)
            .ToArrayAsync(cancellationToken);

    #endregion
}
