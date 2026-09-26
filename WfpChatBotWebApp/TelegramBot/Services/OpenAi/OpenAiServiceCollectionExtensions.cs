using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public static class OpenAiServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiClients(this IServiceCollection services) => services
        .AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            return new ResponsesClient(
                new ApiKeyCredential(options.OpenAiKey),
                new OpenAIClientOptions { Endpoint = OpenAiEndpoint.ForResponses(options.FoundryUrl) });
        });
}
