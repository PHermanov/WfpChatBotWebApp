using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public static class OpenAiServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiClients(this IServiceCollection services)
    {
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FoundryAgentOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.FoundryProjectEndpoint))
                throw new InvalidOperationException("FoundryProjectEndpoint must be configured.");

            var credential = provider.GetRequiredService<TokenCredential>();
            return new AIProjectClient(
                endpoint: new Uri(options.FoundryProjectEndpoint),
                tokenProvider: credential);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FoundryAgentOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.FoundryAgentName) || string.IsNullOrWhiteSpace(options.FoundryAgentVersion))
                throw new InvalidOperationException("FoundryAgentName and FoundryAgentVersion must be configured.");

            provider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(OpenAiServiceCollectionExtensions))
                .LogInformation(
                    "Using Foundry agent {AgentName} v{AgentVersion} at {FoundryProjectEndpoint}",
                    options.FoundryAgentName,
                    options.FoundryAgentVersion,
                    options.FoundryProjectEndpoint);

            return new AgentReference(options.FoundryAgentName, options.FoundryAgentVersion);
        });

        return services;
    }
}
