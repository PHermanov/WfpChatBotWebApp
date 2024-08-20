using Microsoft.Extensions.DependencyInjection;
using WfpChatBotWebApp.Persistence;

namespace IntegrationTests;

[Collection("Integration Tests")]
public class AppDbContextTests : TestBase
{
    private  IGameRepository _repository;
    
    public AppDbContextTests(TestServer server) : base(server)
    {
    }

    [Fact]
    public async Task GameRepository_GetAllChatsIds_ReturnsChats()
    {
        await InitializeAsync();
        _repository = Services.GetRequiredService<IGameRepository>(); 
        var chatIds = await _repository.GetAllChatsIdsAsync(new CancellationToken());
        Assert.True(chatIds.Length > 0);
    }
}