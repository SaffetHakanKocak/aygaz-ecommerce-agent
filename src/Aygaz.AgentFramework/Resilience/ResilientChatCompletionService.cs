#pragma warning disable SKEXP0070

using Aygaz.AgentFramework.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.AgentFramework.Resilience;

public sealed class ResilientChatCompletionService : IChatCompletionService
{
    private readonly IChatCompletionService _inner;
    private readonly SemanticKernelProvider _provider;

    public ResilientChatCompletionService(
        IChatCompletionService inner,
        SemanticKernelProvider provider)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _provider = provider;
    }

    public IReadOnlyDictionary<string, object?> Attributes => _inner.Attributes;

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Microsoft.SemanticKernel.Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return AiProviderRetryExecutor.ExecuteAsync(
            _provider,
            ct => _inner.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, ct),
            cancellationToken);
    }

    public Task<ChatMessageContent> GetChatMessageContentAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Microsoft.SemanticKernel.Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return AiProviderRetryExecutor.ExecuteAsync(
            _provider,
            ct => _inner.GetChatMessageContentAsync(chatHistory, executionSettings, kernel, ct),
            cancellationToken);
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Microsoft.SemanticKernel.Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return AiProviderRetryExecutor.ExecuteStreamingAsync(
            _provider,
            ct => _inner.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, ct),
            cancellationToken);
    }
}
