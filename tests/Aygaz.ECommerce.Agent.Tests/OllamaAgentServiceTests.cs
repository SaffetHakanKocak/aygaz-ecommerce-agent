using System.Text.Json;
using Aygaz.ECommerce.Agent.Agent;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class OllamaAgentServiceTests
{
    [Fact]
    public async Task AskAsync_ToolCall_AppendsAssistantAndToolMessagesBeforeFinalRequest()
    {
        JsonElement arguments = ParseJson(
            """{"email":"ahmet.yilmaz@example.com"}""");
        var toolCall = new OllamaToolCall(
            "call-1",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.GetCustomerByEmailToolName,
                Arguments: arguments));
        var firstAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [toolCall]);
        var finalAssistantMessage = new OllamaChatMessage(
            "assistant",
            "Ahmet Yılmaz, İstanbul'da kayıtlı müşteridir.");
        var chatClient = new RecordingOllamaChatClient(
            firstAssistantMessage,
            finalAssistantMessage);
        ToolExecutionResult toolResult = ToolExecutionResult.FromSuccess(
            new
            {
                id = 1,
                firstName = "Ahmet",
                lastName = "Yılmaz",
                email = "ahmet.yilmaz@example.com",
                city = "İstanbul"
            });
        var toolExecutor = new RecordingAgentToolExecutor(toolResult);
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync(
            "  ahmet.yilmaz@example.com müşterisi kim?  ");

        Assert.Equal("Ahmet Yılmaz, İstanbul'da kayıtlı müşteridir.", answer);
        Assert.Equal(2, chatClient.Calls.Count);
        Assert.Single(toolExecutor.Calls);

        ChatInvocation firstRequest = chatClient.Calls[0];
        Assert.Equal(
            new[] { "system", "user" },
            firstRequest.Messages.Select(message => message.Role));
        Assert.False(string.IsNullOrWhiteSpace(firstRequest.Messages[0].Content));
        Assert.Equal(
            "ahmet.yilmaz@example.com müşterisi kim?",
            firstRequest.Messages[1].Content);

        ChatInvocation secondRequest = chatClient.Calls[1];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool" },
            secondRequest.Messages.Select(message => message.Role));
        Assert.Equal(firstAssistantMessage, secondRequest.Messages[2]);

        OllamaChatMessage toolMessage = secondRequest.Messages[3];
        Assert.Equal(CustomerToolExecutor.GetCustomerByEmailToolName, toolMessage.ToolName);
        Assert.Equal("call-1", toolMessage.ToolCallId);
        Assert.Equal(toolResult.Content, toolMessage.Content);
        Assert.Null(toolMessage.ToolCalls);

        ToolInvocation execution = Assert.Single(toolExecutor.Calls);
        Assert.Equal(CustomerToolExecutor.GetCustomerByEmailToolName, execution.ToolName);
        Assert.Equal(
            "ahmet.yilmaz@example.com",
            execution.Arguments.GetProperty("email").GetString());

        Assert.All(
            chatClient.Calls,
            call => Assert.Same(toolExecutor.ToolDefinitions, call.Tools));
    }

    [Fact]
    public async Task AskAsync_NoToolCall_ReturnsFinalContentWithoutCallingExecutor()
    {
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage("assistant", "  Merhaba! Nasıl yardımcı olabilirim?  "));
        var toolExecutor = new RecordingAgentToolExecutor();
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync("Merhaba");

        Assert.Equal("Merhaba! Nasıl yardımcı olabilirim?", answer);
        Assert.Single(chatClient.Calls);
        Assert.Empty(toolExecutor.Calls);
        Assert.Equal(
            new[] { "system", "user" },
            chatClient.Calls[0].Messages.Select(message => message.Role));
    }

    [Fact]
    public async Task AskAsync_MultipleAllowedToolCalls_ExecutesEachAndPreservesOrder()
    {
        var firstToolCall = new OllamaToolCall(
            "call-email",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.GetCustomerByEmailToolName,
                Arguments: ParseJson("""{"email":"ahmet.yilmaz@example.com"}""")));
        var secondToolCall = new OllamaToolCall(
            "call-id",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.GetCustomerByIdToolName,
                Arguments: ParseJson("""{"id":1}""")));
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage("assistant", null, [firstToolCall, secondToolCall]),
            new OllamaChatMessage("assistant", "İki sorgu tamamlandı."));
        var firstResult = ToolExecutionResult.FromSuccess(new { id = 1 });
        var secondResult = ToolExecutionResult.FromSuccess(new { id = 1 });
        var toolExecutor = new RecordingAgentToolExecutor(firstResult, secondResult);
        var agent = CreateAgent(
            chatClient,
            toolExecutor,
            maxToolCallsPerIteration: 2);

        string answer = await agent.AskAsync("İki müşteri sorgusunu karşılaştır.");

        Assert.Equal("İki sorgu tamamlandı.", answer);
        Assert.Equal(2, toolExecutor.Calls.Count);
        Assert.Equal(
            new[]
            {
                CustomerToolExecutor.GetCustomerByEmailToolName,
                CustomerToolExecutor.GetCustomerByIdToolName
            },
            toolExecutor.Calls.Select(call => call.ToolName));

        ChatInvocation secondRequest = chatClient.Calls[1];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool", "tool" },
            secondRequest.Messages.Select(message => message.Role));
        Assert.Equal("call-email", secondRequest.Messages[3].ToolCallId);
        Assert.Equal("call-id", secondRequest.Messages[4].ToolCallId);
        Assert.Equal(firstResult.Content, secondRequest.Messages[3].Content);
        Assert.Equal(secondResult.Content, secondRequest.Messages[4].Content);
    }

    [Fact]
    public async Task AskAsync_RepeatedToolCalls_StopsAtConfiguredIterationLimit()
    {
        OllamaChatMessage repeatingToolCall = CreateIdToolCallAssistantMessage();
        var chatClient = new RecordingOllamaChatClient(
            repeatingToolCall,
            repeatingToolCall,
            repeatingToolCall);
        var toolExecutor = new RecordingAgentToolExecutor(
            ToolExecutionResult.FromSuccess(new { id = 1 }),
            ToolExecutionResult.FromSuccess(new { id = 1 }));
        var agent = CreateAgent(
            chatClient,
            toolExecutor,
            maxToolIterations: 2);

        AgentException exception = await Assert.ThrowsAsync<AgentException>(
            () => agent.AskAsync("Müşteri sorgusu"));

        Assert.Contains("güvenli", exception.Message);
        Assert.Contains("MaxToolIterations", exception.TechnicalDetails);
        Assert.Equal(3, chatClient.Calls.Count);
        Assert.Equal(2, toolExecutor.Calls.Count);
    }

    [Fact]
    public async Task AskAsync_TooManyToolCallsInOneResponse_RejectsBeforeExecution()
    {
        OllamaToolCall[] toolCalls = Enumerable.Range(1, 3)
            .Select(index => new OllamaToolCall(
                $"call-{index}",
                new OllamaToolCallFunction(
                    Name: CustomerToolExecutor.GetCustomerByIdToolName,
                    Arguments: ParseJson($$"""{"id":{{index}}}"""))))
            .ToArray();
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage("assistant", null, toolCalls));
        var toolExecutor = new RecordingAgentToolExecutor();
        var agent = CreateAgent(
            chatClient,
            toolExecutor,
            maxToolCallsPerIteration: 2);

        AgentException exception = await Assert.ThrowsAsync<AgentException>(
            () => agent.AskAsync("Üç müşteri sorgula"));

        Assert.Contains("güvenli", exception.Message);
        Assert.Empty(toolExecutor.Calls);
        Assert.Single(chatClient.Calls);
    }

    [Fact]
    public async Task AskAsync_ToolCallWithMissingArguments_FailsSafelyBeforeExecution()
    {
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage(
                "assistant",
                null,
                [
                    new OllamaToolCall(
                        "call-invalid",
                        new OllamaToolCallFunction(
                            Name: CustomerToolExecutor.GetCustomerByEmailToolName))
                ]));
        var toolExecutor = new RecordingAgentToolExecutor();
        var agent = CreateAgent(chatClient, toolExecutor);

        AgentException exception = await Assert.ThrowsAsync<AgentException>(
            () => agent.AskAsync("Müşteri sorgusu"));

        Assert.Contains("geçerli bir tool çağrısı", exception.Message);
        Assert.Contains("arguments", exception.TechnicalDetails);
        Assert.Empty(toolExecutor.Calls);
        Assert.Single(chatClient.Calls);
    }

    private static OllamaAgentService CreateAgent(
        IOllamaChatClient chatClient,
        IAgentToolExecutor toolExecutor,
        int maxToolIterations = 5,
        int maxToolCallsPerIteration = 3)
    {
        return new OllamaAgentService(
            chatClient,
            toolExecutor,
            Options.Create(new AgentOptions
            {
                MaxToolIterations = maxToolIterations,
                MaxToolCallsPerIteration = maxToolCallsPerIteration,
                MaxConversationTurns = 4,
                MaxNameSearchResults = 5
            }));
    }

    private static OllamaChatMessage CreateIdToolCallAssistantMessage()
    {
        return new OllamaChatMessage(
            "assistant",
            null,
            [
                new OllamaToolCall(
                    "call-id",
                    new OllamaToolCallFunction(
                        Name: CustomerToolExecutor.GetCustomerByIdToolName,
                        Arguments: ParseJson("""{"id":1}""")))
            ]);
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class RecordingOllamaChatClient(
        params OllamaChatMessage[] responses) : IOllamaChatClient
    {
        private readonly Queue<OllamaChatMessage> _responses = new(responses);

        public List<ChatInvocation> Calls { get; } = [];

        public Task<OllamaChatMessage> ChatAsync(
            IReadOnlyCollection<OllamaChatMessage> messages,
            IReadOnlyCollection<OllamaToolDefinition>? tools = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new ChatInvocation(messages.ToArray(), tools));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("Test Ollama response queue is empty.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class RecordingAgentToolExecutor(
        params ToolExecutionResult[] results) : IAgentToolExecutor
    {
        private readonly Queue<ToolExecutionResult> _results = new(results);

        public IReadOnlyList<OllamaToolDefinition> ToolDefinitions { get; } =
        [
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    CustomerToolExecutor.GetCustomerByEmailToolName,
                    "Test tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["email"] = new("string", "Test email")
                        },
                        ["email"],
                        AdditionalProperties: false)))
        ];

        public List<ToolInvocation> Calls { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(
            string? toolName,
            JsonElement arguments,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement argumentsSnapshot = arguments.ValueKind == JsonValueKind.Undefined
                ? default
                : arguments.Clone();
            Calls.Add(new ToolInvocation(toolName, argumentsSnapshot));

            if (_results.Count == 0)
            {
                throw new InvalidOperationException("Test tool result queue is empty.");
            }

            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed record ChatInvocation(
        IReadOnlyList<OllamaChatMessage> Messages,
        IReadOnlyCollection<OllamaToolDefinition>? Tools);

    private sealed record ToolInvocation(string? ToolName, JsonElement Arguments);
}
