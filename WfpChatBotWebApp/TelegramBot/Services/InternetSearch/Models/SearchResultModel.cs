using System.Text.Json.Serialization;

namespace WfpChatBotWebApp.TelegramBot.Services.InternetSearch.Models;

public record SearchResultModel
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; init; } = string.Empty;
}
