using Microsoft.Extensions.DependencyInjection;
using WfpChatBotWebApp.Persistence;

namespace IntegrationTests;

[Collection("Integration Tests")]
public class AppDbContextTests(TestServer server) : TestBase(server)
{
    [Fact]
    public async Task GameRepository_GetAllChatsIds_ReturnsChats()
    {
        await InitializeAsync();
        var repository = Services.GetRequiredService<IGameRepository>(); 
        var chatIds = await repository.GetAllChatsIdsAsync(new CancellationToken());
        Assert.True(chatIds.Length > 0);
    }
    
    [Fact]
    public async Task GameRepository_GetAllResults_ReturnsResults()
    {
        await InitializeAsync();
        var repository = Services.GetRequiredService<IGameRepository>(); 
        var chatIds = await repository.GetAllChatsIdsAsync(new CancellationToken());
        var winners = await repository.GetAllWinnersAsync(chatIds[0], new CancellationToken());
        Assert.True(winners.Length > 0);
    }
}