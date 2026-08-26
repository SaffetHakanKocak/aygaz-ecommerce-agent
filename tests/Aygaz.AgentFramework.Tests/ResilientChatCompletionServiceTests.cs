#pragma warning disable SKEXP0070

using System.Net;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Resilience;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.AgentFramework.Tests;

public sealed class ResilientChatCompletionServiceTests
{
    [Fact]
    public async Task Transient429_RetriesOnceThenSucceeds()
    {
        var inner = new ScriptedChatCompletionService(
            new HttpOperationException("rate limited")
            {
                StatusCode = HttpStatusCode.TooManyRequests
            },
            new ChatMessageContent(AuthorRole.Assistant, "ok"));

        var resilient = new ResilientChatCompletionService(inner, SemanticKernelProvider.Groq);
        ChatMessageContent result = await resilient.GetChatMessageContentAsync(new ChatHistory());

        Assert.Equal("ok", result.Content);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Transient429_SecondFailure_ReturnsControlledUnavailableException()
    {
        var failure = new HttpOperationException("rate limited")
        {
            StatusCode = HttpStatusCode.TooManyRequests
        };
        var inner = new ScriptedChatCompletionService(failure, failure);

        var resilient = new ResilientChatCompletionService(inner, SemanticKernelProvider.Groq);

        AiProviderTemporarilyUnavailableException ex = await Assert.ThrowsAsync<AiProviderTemporarilyUnavailableException>(
            () => resilient.GetChatMessageContentAsync(new ChatHistory()));

        Assert.Equal(AiProviderErrorType.RateLimit, ex.Details.ErrorType);
        Assert.Equal(429, ex.Details.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Authentication401_DoesNotRetry()
    {
        var inner = new ScriptedChatCompletionService(
            new HttpOperationException("unauthorized")
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        var resilient = new ResilientChatCompletionService(inner, SemanticKernelProvider.Groq);

        HttpOperationException ex = await Assert.ThrowsAsync<HttpOperationException>(
            () => resilient.GetChatMessageContentAsync(new ChatHistory()));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task Transient503_OnNonGroqProvider_DoesNotRetry()
    {
        var inner = new ScriptedChatCompletionService(
            new HttpOperationException("unavailable")
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            });

        var resilient = new ResilientChatCompletionService(inner, SemanticKernelProvider.OpenAI);

        await Assert.ThrowsAsync<HttpOperationException>(
            () => resilient.GetChatMessageContentAsync(new ChatHistory()));

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task TransientHttpRequestException_RetriesOnce()
    {
        var inner = new ScriptedChatCompletionService(
            new HttpRequestException("connection reset"),
            new ChatMessageContent(AuthorRole.Assistant, "ok"));

        var resilient = new ResilientChatCompletionService(inner, SemanticKernelProvider.Groq);
        ChatMessageContent result = await resilient.GetChatMessageContentAsync(new ChatHistory());

        Assert.Equal("ok", result.Content);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public void GroqKernel_WrapsChatCompletionWithResilience()
    {
        using var _ = EnvVarScope.Set(SemanticKernelFactory.GroqApiKeyVariableName, "gsk_test-not-a-real-key");

        var factory = new SemanticKernelFactory(new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.Groq,
            ModelId = "openai/gpt-oss-120b"
        });

        IChatCompletionService service = factory.CreateKernel().GetRequiredService<IChatCompletionService>();
        Assert.IsType<ResilientChatCompletionService>(service);
    }

    private sealed class ScriptedChatCompletionService : IChatCompletionService
    {
        private readonly Queue<object> _script;

        public ScriptedChatCompletionService(params object[] script)
        {
            _script = new Queue<object>(script);
        }

        public int CallCount { get; private set; }

        public IReadOnlyDictionary<string, object?> Attributes { get; } =
            new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            return GetChatMessageContentAsync(chatHistory, executionSettings, kernel, cancellationToken)
                .ContinueWith(
                    task => (IReadOnlyList<ChatMessageContent>)[task.Result],
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        public Task<ChatMessageContent> GetChatMessageContentAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            object next = _script.Dequeue();
            if (next is Exception ex)
            {
                throw ex;
            }

            return Task.FromResult((ChatMessageContent)next);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
