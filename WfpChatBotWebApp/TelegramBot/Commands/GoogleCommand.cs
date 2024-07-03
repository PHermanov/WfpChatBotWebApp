using MediatR;
using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class GoogleCommand(Message message) : CommandWithParam(message), IRequest;

public class GoogleCommandHandler(
    ITelegramBotClient botClient,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ITextMessageService textMessageService,
    ILogger<GoogleCommandHandler> logger)
    : IRequestHandler<GoogleCommand>
{
    public async Task Handle(GoogleCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Inside Google command");
        if (string.IsNullOrEmpty(request.Param))
        {
            logger.LogInformation("Search query is empty");

            var responsePhrase = await textMessageService.GetMessageByNameAsync(TextMessageNames.WhatWanted, cancellationToken);

            if (string.IsNullOrEmpty(responsePhrase))
                return;

            await botClient.TrySendTextMessageAsync(request.ChatId, $"{request.FromMention} *{responsePhrase}*", ParseMode.Markdown, cancellationToken: cancellationToken);

            return;
        }

        var googleKeys = configuration["GoogleApiKey"];

        if (string.IsNullOrEmpty(googleKeys))
        {
            logger.LogError("GoogleApiKey is null");
            return;
        }

        var split = googleKeys.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length < 2 || string.IsNullOrEmpty(split[0]) || string.IsNullOrEmpty(split[1]))
        {
            logger.LogError("GoogleApiKey corrupted");
            return;
        }

        var urlParams = $"v1?key={split[0]}&cx={split[1]}&q={request.Param.Trim()}";

        var httpClient = httpClientFactory.CreateClient("Google");
        var httpResponseMessage = await httpClient.GetAsync(urlParams, cancellationToken);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            logger.LogInformation("Google returned not success status");
            return;
        }

        await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken);

        var searchResults = await JsonSerializer.DeserializeAsync<GoogleResponseModel>(contentStream, cancellationToken: cancellationToken);
        if (searchResults == null)
        {
            logger.LogInformation("Parsed 0 results");
            return;
        }

        logger.LogInformation("Parsed {searchResultsCount} results", searchResults.Items.Count);

        var resultItems = searchResults.Items.Take(3).ToArray();

        foreach (var result in resultItems)
        {
            var msg = $"{result.Title}{Environment.NewLine}{result.Link}";

            await botClient.TrySendTextMessageAsync(request.ChatId, msg, cancellationToken: cancellationToken);
        }
    }
}

public class SearchResultModel
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; init; } = string.Empty;
}

public class GoogleResponseModel
{
    [JsonPropertyName("items")]
    public List<SearchResultModel> Items { get; set; } = new();
}
