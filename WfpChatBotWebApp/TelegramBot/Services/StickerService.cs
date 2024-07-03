using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IStickerService
{
    Task<string> GetRandomStickerFromSet(string set, CancellationToken cancellationToken);
}

public class StickerService(IGameRepository repository, IConfiguration configuration) : IStickerService
{
    public async Task<string> GetRandomStickerFromSet(string set, CancellationToken cancellationToken)
    {
        var stickers = await repository.GetStickersBySetAsync(set, cancellationToken);

        return stickers.Length != 0 
            ? stickers[new Random().Next(stickers.Length)].Url + configuration.GetValue<string>("StickerSas")
            : string.Empty;
    }
    
    
    public static class StickerSet
    {
        public const string Mamota = nameof(Mamota);
        public const string Yoba = nameof(Yoba);
        public const string Frog = nameof(Frog);
    }
}