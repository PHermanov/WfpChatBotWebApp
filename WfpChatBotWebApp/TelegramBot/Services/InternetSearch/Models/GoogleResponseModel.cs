using System.Text.Json.Serialization;

namespace WfpChatBotWebApp.TelegramBot.Services.InternetSearch.Models;

public record GoogleResponseModel
{
    [JsonPropertyName("items")]
    public List<SearchResultModel> Items { get; set; } = new();
}