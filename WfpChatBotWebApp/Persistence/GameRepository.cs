﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.Persistence.Models;

namespace WfpChatBotWebApp.Persistence;
public class GameRepository(AppDbContext context, IMemoryCache cache) : IGameRepository
{
    public async Task CheckUserAsync(long chatId, long userId, string userName)
    {
        if (cache.TryGetValue((chatId, userId), out bool saved) && saved)
        {
            return;
        }

        var userById = await GetUserByUserIdAsync(chatId, userId);

        if (userById != null)
        {
            cache.Set((chatId, userId), true, TimeSpan.FromDays(30));

            if (userById.Inactive)
            {
                userById.Inactive = false;
                userById.UserName = userName;
                await context.SaveChangesAsync();
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

            await context.BotUsers.AddAsync(newUser);
            await context.SaveChangesAsync();

            cache.Set((chatId, userId), true, TimeSpan.FromDays(30));
        }
    }

    public async Task<List<BotUser>> GetAllUsersAsync(long chatId)
        => await context.BotUsers.Where(p => p.ChatId == chatId && p.Inactive == false).ToListAsync();

    public async Task<List<BotUser>> GetActiveUsersAsync(long chatId)
        => await context.BotUsers.Where(p => p.ChatId == chatId && p.Inactive == false).ToListAsync();
    
    public async Task<BotUser?> GetUserByUserIdAsync(long chatId, long userId)
        => await context.BotUsers.FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == userId);

    public async Task<BotUser?> GetUserByNameAsync(long chatId, string userName)
        => await context.BotUsers.FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserName == userName);

    public async Task<Result?> GetTodayResultAsync(long chatId)
        => await context.Results.FirstOrDefaultAsync(r => r.ChatId == chatId && r.PlayedAt.Date == DateTime.Today);

    public async Task<Result?> GetYesterdayResultAsync(long chatId)
        => await context.Results.FirstOrDefaultAsync(r => r.ChatId == chatId && r.PlayedAt.Date == DateTime.Today.AddDays(-1));

    public async Task<Result?> GetLastPlayedGameAsync(long chatId)
        => await context.Results
            .Where(r => r.ChatId == chatId)
            .OrderByDescending(r => r.PlayedAt)
            .FirstOrDefaultAsync();

    public async Task SaveResultAsync(Result result)
    {
        await context.Results.AddAsync(result);
        await context.SaveChangesAsync();
    }

    public async Task<List<PlayerCountViewModel>> GetAllWinnersForMonthAsync(long chatId, DateTime date)
        => await GetMonthResults(chatId, date).ToListAsync();

    public async Task<PlayerCountViewModel?> GetWinnerForMonthAsync(long chatId, DateTime date)
        => await GetMonthResults(chatId, date).FirstOrDefaultAsync();

    public async Task<long[]> GetAllChatsIdsAsync()
        => await context.BotUsers.Select(p => p.ChatId)
            .Distinct()
            .ToArrayAsync();

    public async Task<List<PlayerCountViewModel>> GetAllWinnersAsync(long chatId)
        => await context.Results.Where(r => r.ChatId == chatId)
            .ApplyGrouping()
            .ToListAsync();

    private IOrderedQueryable<PlayerCountViewModel> GetMonthResults(long chatId, DateTime date)
        => context.Results
            .Where(r => r.ChatId == chatId && r.PlayedAt.Date.Year == date.Year && r.PlayedAt.Date.Month == date.Month)
            .ApplyGrouping();

    public async Task<PlayerCountViewModel?> GetYearWinnerByCountAsync(long chatId, int year)
        => await context.Results
            .Where(r => r.ChatId == chatId && r.PlayedAt.Date.Year == year)
            .ApplyGrouping()
            .FirstOrDefaultAsync();

    public async Task<List<PlayerCountViewModel>> GetAllWinnersForYearAsync(long chatId, int year)
        => await context.Results
            .Where(r => r.ChatId == chatId && r.PlayedAt.Date.Year == year)
            .ApplyGrouping()
            .ToListAsync();
}
