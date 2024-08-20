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
            return;

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
            var newUser = new User
            {
                ChatId = chatId,
                UserId = userId,
                UserName = userName
            };

            await context.Users.AddAsync(newUser, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            cache.Set((chatId, userId), true, TimeSpan.FromDays(30));
        }
    }

    public async Task<long[]> GetAllChatsIdsAsync(CancellationToken cancellationToken)
        => await context.Users
            .Where(p => p.ChatId < 0) // chats ids are negative
            .Select(p => p.ChatId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    
    public async Task<User[]> GetActiveUsersForChatAsync(long chatId, CancellationToken cancellationToken)
        => await context.Users.Where(p => p.ChatId == chatId && p.Inactive == false).ToArrayAsync(cancellationToken);
    
    public async Task<User[]> GetInactiveUsersAsync(long chatId, CancellationToken cancellationToken)
        => await context.Users.Where(p => p.ChatId == chatId && p.Inactive == true).ToArrayAsync(cancellationToken);

    public async Task<User[]> GetAllUsersForChat(long chatId, CancellationToken cancellationToken)
        => await context.Users.Where(p => p.ChatId == chatId).ToArrayAsync(cancellationToken);
    
    public async Task<User?> GetUserByUserIdAndChatIdAsync(long chatId, long userId, CancellationToken cancellationToken)
        => await context.Users.FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId, cancellationToken);

    public async Task<User?> GetUserByNameAsync(long chatId, string userName)
        => await context.Users.FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserName == userName);

    #endregion

    #region Results
    
    public async Task<Result?> GetTodayResultAsync(long chatId, CancellationToken cancellationToken)
        => await context.Results.FirstOrDefaultAsync(r => r.ChatId == chatId && r.PlayDate.Date == DateTime.Today, cancellationToken);

    public async Task<Result?> GetYesterdayResultAsync(long chatId, CancellationToken cancellationToken)
        => await context.Results.FirstOrDefaultAsync(r => r.ChatId == chatId && r.PlayDate.Date == DateTime.Today.AddDays(-1), cancellationToken);

    public async Task<Result?> GetLastPlayedGameAsync(long chatId, CancellationToken cancellationToken)
        => await context.Results
            .Where(r => r.ChatId == chatId)
            .OrderByDescending(r => r.PlayDate)
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
            .Where(r => r.ChatId == chatId && r.PlayDate.Date.Year == date.Year && r.PlayDate.Date.Month == date.Month)
            .ApplyGrouping();

    public async Task<PlayerCountViewModel?> GetYearWinnerByCountAsync(long chatId, int year, CancellationToken cancellationToken)
        => await context.Results
            .Where(r => r.ChatId == chatId && r.PlayDate.Date.Year == year)
            .ApplyGrouping()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PlayerCountViewModel[]> GetAllWinnersForYearAsync(long chatId, int year, CancellationToken cancellationToken)
        => await context.Results
            .Where(r => r.ChatId == chatId && r.PlayDate.Date.Year == year)
            .ApplyGrouping()
            .ToArrayAsync(cancellationToken);
    #endregion

    #region Stickers

    public async Task<Sticker[]> GetStickersBySetAsync(string set, CancellationToken cancellationToken)
        => await context.Stickers
            .Where(s => s.StickerSet == set)
            .ToArrayAsync(cancellationToken);

    public async Task<Sticker?> GetImageByNameAsync(string name, CancellationToken cancellationToken)
        => await context.Stickers
            .FirstOrDefaultAsync(s => s.Name == name, cancellationToken);

    #endregion
}
