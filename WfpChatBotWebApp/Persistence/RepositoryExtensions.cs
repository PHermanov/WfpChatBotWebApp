using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.Persistence.Models;

namespace WfpChatBotWebApp.Persistence;

public static class RepositoryExtensions
{
    public static IOrderedQueryable<PlayerCountViewModel> ApplyGrouping(this IQueryable<Result> source)
        => source
            .GroupBy(g => new { g.UserId })
            .Select(gr => new PlayerCountViewModel
                {
                    UserId = gr.Key.UserId,
                    Count = gr.Count(),
                    LastWin = gr.Max(r => r.PlayDate)
                })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.LastWin);
}