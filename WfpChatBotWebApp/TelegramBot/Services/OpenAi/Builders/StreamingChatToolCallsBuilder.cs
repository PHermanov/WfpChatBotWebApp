using System.Buffers;
using OpenAI.Chat;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Builders;

/// <summary>
/// Copied from examples repo
/// <see href="https://github.com/openai/openai-dotnet/blob/31c2ba63c625b1b4fc2640ddf378a97e89b89167/examples/Chat/Example04_FunctionCallingStreaming.cs#L18-L70" />
/// Probably will be a part of OpenAI package in future versions
/// </summary>
[Obsolete("Check presence in latest OpenAI package.")]
internal class StreamingChatToolCallsBuilder
{
    private readonly Dictionary<int, string> _indexToToolCallId = [];
    private readonly Dictionary<int, string> _indexToFunctionName = [];
    private readonly Dictionary<int, SequenceBuilder<byte>> _indexToFunctionArguments = [];

    public void Append(StreamingChatToolCallUpdate toolCallUpdate)
    {
        // Keep track of which tool call ID belongs to this update index.
        if (toolCallUpdate.ToolCallId != null)
        {
            _indexToToolCallId[toolCallUpdate.Index] = toolCallUpdate.ToolCallId;
        }

        // Keep track of which function name belongs to this update index.
        if (toolCallUpdate.FunctionName != null)
        { 
            _indexToFunctionName[toolCallUpdate.Index] = toolCallUpdate.FunctionName;
        }

        // Keep track of which function arguments belong to this update index,
        // and accumulate the arguments as new updates arrive.
        if (toolCallUpdate.FunctionArgumentsUpdate != null && !toolCallUpdate.FunctionArgumentsUpdate.ToMemory().IsEmpty)
        {
            if (!_indexToFunctionArguments.TryGetValue(toolCallUpdate.Index, out var argumentsBuilder))
            {
                argumentsBuilder = new SequenceBuilder<byte>();
                _indexToFunctionArguments[toolCallUpdate.Index] = argumentsBuilder;
            }

            argumentsBuilder.Append(toolCallUpdate.FunctionArgumentsUpdate);
        }
    }

    public IReadOnlyList<ChatToolCall> Build()
    {
        List<ChatToolCall> toolCalls = [];

        foreach (var (index, toolCallId) in _indexToToolCallId)
        {
            var sequence = _indexToFunctionArguments[index].Build();

            var toolCall = ChatToolCall.CreateFunctionToolCall(
                id: toolCallId,
                functionName: _indexToFunctionName[index],
                functionArguments: BinaryData.FromBytes(sequence.ToArray()));

            toolCalls.Add(toolCall);
        }

        return toolCalls;
    }
}
