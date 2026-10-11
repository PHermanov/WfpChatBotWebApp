using System.ClientModel.Primitives;
using System.Globalization;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

// Foundry resolves the memory tool scope {{$userId}} from this header; without it every chat
// falls back to the caller's Entra identity and shares one memory scope.
public sealed class MemoryScopePolicy : PipelinePolicy
{
    public const string HeaderName = "x-memory-user-id";

    private static readonly AsyncLocal<string?> CurrentScope = new();

    public static string ForChat(long chatId) =>
        string.Create(CultureInfo.InvariantCulture, $"telegram_chat_{chatId}");

    public static IDisposable Use(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var previous = CurrentScope.Value;
        CurrentScope.Value = scope;
        return new ScopeReset(previous);
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Apply(message);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }

    private static void Apply(PipelineMessage message)
    {
        if (CurrentScope.Value is { } scope)
            message.Request.Headers.Set(HeaderName, scope);
    }

    private sealed class ScopeReset(string? previous) : IDisposable
    {
        public void Dispose() => CurrentScope.Value = previous;
    }
}
