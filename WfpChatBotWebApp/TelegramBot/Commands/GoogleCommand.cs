﻿using MediatR;
using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class GoogleCommand : CommandWithParam, IRequest
{
    public GoogleCommand(Message message) : base(message) { }
}

public class GoogleCommandHandler : IRequestHandler<GoogleCommand>
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITextMessageService _textMessageService;
    private readonly ILogger<GoogleCommandHandler> _logger;

    public GoogleCommandHandler(
        ITelegramBotClient botClient,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ITextMessageService textMessageService,
        ILogger<GoogleCommandHandler> logger)
    {
        _botClient = botClient;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _textMessageService = textMessageService;
        _logger = logger;
    }

    public async Task Handle(GoogleCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Inside Google command");
        if (string.IsNullOrEmpty(request.Param))
        {
            _logger.LogInformation("Search query is empty");

            var responsePhrase = await _textMessageService.GetMessageByNameAsync(TextMessageNames.WhatWanted, cancellationToken);

            if (string.IsNullOrEmpty(responsePhrase))
                return;

            await _botClient.TrySendTextMessageAsync(request.ChatId, $"{request.FromMention} *{responsePhrase}*", ParseMode.Markdown, cancellationToken: cancellationToken);

            return;
        }

        var googleKeys = _configuration["GoogleApiKey"];

        if (string.IsNullOrEmpty(googleKeys))
        {
            _logger.LogError("GoogleApiKey is null");
            return;
        }

        var split = googleKeys.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length < 2 || string.IsNullOrEmpty(split[0]) || string.IsNullOrEmpty(split[1]))
        {
            _logger.LogError("GoogleApiKey corrupted");
            return;
        }

        var urlParams = $"v1?key={split[0]}&cx={split[1]}&q={request.Param.Trim()}";

        var httpClient = _httpClientFactory.CreateClient("Google");
        var httpResponseMessage = await httpClient.GetAsync(urlParams, cancellationToken);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            _logger.LogInformation("Google returned not success status");
            return;
        }

        await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken);

        var searchResults = await JsonSerializer.DeserializeAsync<GoogleResponseModel>(contentStream, cancellationToken: cancellationToken);
        if (searchResults == null)
        {
            _logger.LogInformation("Parsed 0 results");
            return;
        }

        _logger.LogInformation("Parsed {searchResultsCount} results", searchResults.Items.Count);

        var resultItems = searchResults.Items.Take(3).ToArray();

        foreach (var result in resultItems)
        {
            var msg = $"{result.Title}{Environment.NewLine}{result.Link}";

            await _botClient.TrySendTextMessageAsync(request.ChatId, msg, cancellationToken: cancellationToken);
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
