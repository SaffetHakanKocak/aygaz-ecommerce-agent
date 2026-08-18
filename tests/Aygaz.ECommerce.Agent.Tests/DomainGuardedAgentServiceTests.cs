using System.Text.Json;
using Aygaz.ECommerce.Agent.Agent;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Guardrails;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class DomainGuardedAgentServiceTests
{
    [Fact]
    public async Task AskAsync_Allowed_CallsRawAgentOnceWithTrimmedInput()
    {
        var guardrail = new StubDomainGuardrailService(
            new DomainScopeResult(
                DomainScopeDecision.Allowed,
                DomainScopeReasonCode.ClassifiedAllowed));
        var rawAgent = new RecordingAgentService("Agent yanıtı");
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

        string answer = await guardedAgent.AskAsync(
            "  ahmet.yilmaz@example.com müşterisi kim?  ");

        Assert.Equal("Agent yanıtı", answer);
        Assert.Equal(1, guardrail.CallCount);
        Assert.Equal(
            "ahmet.yilmaz@example.com müşterisi kim?",
            guardrail.LastInput);
        Assert.Equal(1, rawAgent.CallCount);
        Assert.Equal(
            "ahmet.yilmaz@example.com müşterisi kim?",
            rawAgent.LastInput);
        Assert.Equal(
            new GuardrailLogEntry(
                DomainScopeDecision.Allowed,
                DomainScopeReasonCode.ClassifiedAllowed),
            Assert.Single(logger.Entries));
    }

    [Fact]
    public async Task AskAsync_OutOfScope_ReturnsControlledResponseWithoutCallingRawAgent()
    {
        var guardrail = new StubDomainGuardrailService(
            new DomainScopeResult(
                DomainScopeDecision.OutOfScope,
                DomainScopeReasonCode.ClassifiedOutOfScope));
        var rawAgent = new RecordingAgentService("must not be returned");
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

        string answer = await guardedAgent.AskAsync("Arçelik hakkında bilgi ver.");

        Assert.Equal(DomainGuardedAgentService.OutOfScopeResponse, answer);
        Assert.Equal(1, guardrail.CallCount);
        Assert.Equal(0, rawAgent.CallCount);
        Assert.Equal(
            new GuardrailLogEntry(
                DomainScopeDecision.OutOfScope,
                DomainScopeReasonCode.ClassifiedOutOfScope),
            Assert.Single(logger.Entries));
    }

    [Fact]
    public async Task AskAsync_Ambiguous_ReturnsClarificationWithoutCallingRawAgent()
    {
        var guardrail = new StubDomainGuardrailService(
            new DomainScopeResult(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifiedAmbiguous));
        var rawAgent = new RecordingAgentService("must not be returned");
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

        string answer = await guardedAgent.AskAsync("Bunun durumunu kontrol et.");

        Assert.Equal(DomainGuardedAgentService.AmbiguousResponse, answer);
        Assert.Equal(1, guardrail.CallCount);
        Assert.Equal(0, rawAgent.CallCount);
        Assert.Equal(
            new GuardrailLogEntry(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifiedAmbiguous),
            Assert.Single(logger.Entries));
    }

    [Fact]
    public async Task AskAsync_GuardrailFailure_FailsClosedWithoutCallingRawAgent()
    {
        var guardrail = new StubDomainGuardrailService(
            new InvalidOperationException("classifier failure"));
        var rawAgent = new RecordingAgentService("must not be returned");
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

        string answer = await guardedAgent.AskAsync("Aygaz müşteri sorgusu");

        Assert.Equal(DomainGuardedAgentService.AmbiguousResponse, answer);
        Assert.Equal(1, guardrail.CallCount);
        Assert.Equal(0, rawAgent.CallCount);
        Assert.Equal(
            new GuardrailLogEntry(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifierUnavailable),
            Assert.Single(logger.Entries));
    }

    [Fact]
    public async Task AskAsync_UnknownOrInconsistentDecision_FailsClosedWithoutCallingRawAgent()
    {
        DomainScopeResult[] invalidResults =
        [
            new(
                (DomainScopeDecision)999,
                DomainScopeReasonCode.ClassifiedAllowed),
            new(
                DomainScopeDecision.Allowed,
                DomainScopeReasonCode.ClassifiedOutOfScope),
            new(
                DomainScopeDecision.OutOfScope,
                DomainScopeReasonCode.ClassifiedAllowed),
            new(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifiedAllowed)
        ];

        foreach (DomainScopeResult invalidResult in invalidResults)
        {
            var guardrail = new StubDomainGuardrailService(invalidResult);
            var rawAgent = new RecordingAgentService("must not be returned");
            var logger = new RecordingDomainGuardrailLogger();
            var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

            string answer = await guardedAgent.AskAsync("Aygaz müşteri sorgusu");

            Assert.Equal(DomainGuardedAgentService.AmbiguousResponse, answer);
            Assert.Equal(0, rawAgent.CallCount);
            Assert.Equal(
                new GuardrailLogEntry(
                    DomainScopeDecision.Ambiguous,
                    DomainScopeReasonCode.ClassifierResponseInvalid),
                Assert.Single(logger.Entries));
        }
    }

    [Fact]
    public async Task AskAsync_NullGuardrailResult_FailsClosedWithoutCallingRawAgent()
    {
        var guardrail = new NullDomainGuardrailService();
        var rawAgent = new RecordingAgentService("must not be returned");
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

        string answer = await guardedAgent.AskAsync("Aygaz müşteri sorgusu");

        Assert.Equal(DomainGuardedAgentService.AmbiguousResponse, answer);
        Assert.Equal(0, rawAgent.CallCount);
        Assert.Equal(
            new GuardrailLogEntry(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifierResponseInvalid),
            Assert.Single(logger.Entries));
    }

    [Fact]
    public async Task AskAsync_BlockedRequest_DoesNotReachAgentChatOrToolExecutor()
    {
        var guardrail = new StubDomainGuardrailService(
            new DomainScopeResult(
                DomainScopeDecision.OutOfScope,
                DomainScopeReasonCode.ClassifiedOutOfScope));
        var agentChatClient = new RecordingAgentChatClient(
            CreateCustomerToolCallResponse());
        var toolExecutor = new RecordingAgentToolExecutor();
        var rawAgent = new OllamaAgentService(
            agentChatClient,
            toolExecutor,
            Options.Create(new AgentOptions
            {
                MaxToolIterations = 5,
                MaxToolCallsPerIteration = 3,
                MaxNameSearchResults = 5,
                MaxConversationTurns = 4
            }));
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

        string answer = await guardedAgent.AskAsync("Arçelik müşterilerini getir.");

        Assert.Equal(DomainGuardedAgentService.OutOfScopeResponse, answer);
        Assert.Equal(0, agentChatClient.CallCount);
        Assert.Equal(0, toolExecutor.CallCount);
    }

    [Fact]
    public async Task AskAsync_UnsupportedButAygazRelatedCapability_RemainsAllowed()
    {
        var guardrail = new StubDomainGuardrailService(
            new DomainScopeResult(
                DomainScopeDecision.Allowed,
                DomainScopeReasonCode.ClassifiedAllowed));
        var rawAgent = new RecordingAgentService(
            "Stok sorgulama yeteneği henüz mevcut değil.");
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);

        string answer = await guardedAgent.AskAsync("Aygaz ürün stoklarını göster.");

        Assert.Equal("Stok sorgulama yeteneği henüz mevcut değil.", answer);
        Assert.Equal(1, rawAgent.CallCount);
        Assert.Equal("Aygaz ürün stoklarını göster.", rawAgent.LastInput);
    }

    [Fact]
    public async Task AskAsync_CallerCancellation_IsPropagatedWithoutCallingRawAgent()
    {
        var guardrail = new StubDomainGuardrailService(
            new DomainScopeResult(
                DomainScopeDecision.Allowed,
                DomainScopeReasonCode.ClassifiedAllowed));
        var rawAgent = new RecordingAgentService("must not be returned");
        var logger = new RecordingDomainGuardrailLogger();
        var guardedAgent = new DomainGuardedAgentService(guardrail, rawAgent, logger);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => guardedAgent.AskAsync(
                "Aygaz müşteri sorgusu",
                cancellationTokenSource.Token));

        Assert.Equal(1, guardrail.CallCount);
        Assert.Equal(0, rawAgent.CallCount);
        Assert.Empty(logger.Entries);
    }

    private static OllamaChatMessage CreateCustomerToolCallResponse()
    {
        return new OllamaChatMessage(
            "assistant",
            null,
            [
                new OllamaToolCall(
                    "call-1",
                    new OllamaToolCallFunction(
                        Name: CustomerToolExecutor.GetCustomerByEmailToolName,
                        Arguments: ParseJson(
                            """{"email":"ahmet.yilmaz@example.com"}""")))
            ]);
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class StubDomainGuardrailService : IDomainGuardrailService
    {
        private readonly DomainScopeResult? _result;
        private readonly Exception? _exception;

        public StubDomainGuardrailService(DomainScopeResult result)
        {
            _result = result;
        }

        public StubDomainGuardrailService(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount { get; private set; }

        public string? LastInput { get; private set; }

        public Task<DomainScopeResult> EvaluateAsync(
            string? userInput,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastInput = userInput;
            cancellationToken.ThrowIfCancellationRequested();

            if (_exception is not null)
            {
                return Task.FromException<DomainScopeResult>(_exception);
            }

            return Task.FromResult(
                _result
                ?? throw new InvalidOperationException("Test guardrail result is missing."));
        }
    }

    private sealed class RecordingAgentService(string response) : IAgentService
    {
        public int CallCount { get; private set; }

        public string? LastInput { get; private set; }

        public Task<string> AskAsync(
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastInput = userMessage;
            return Task.FromResult(response);
        }
    }

    private sealed class NullDomainGuardrailService : IDomainGuardrailService
    {
        public Task<DomainScopeResult> EvaluateAsync(
            string? userInput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<DomainScopeResult>(null!);
        }
    }

    private sealed class RecordingDomainGuardrailLogger : IDomainGuardrailLogger
    {
        public List<GuardrailLogEntry> Entries { get; } = [];

        public void LogDecision(
            DomainScopeDecision decision,
            DomainScopeReasonCode reasonCode)
        {
            Entries.Add(new GuardrailLogEntry(decision, reasonCode));
        }
    }

    private sealed class RecordingAgentChatClient(
        OllamaChatMessage response) : IOllamaChatClient
    {
        public int CallCount { get; private set; }

        public Task<OllamaChatMessage> ChatAsync(
            IReadOnlyCollection<OllamaChatMessage> messages,
            OllamaChatSettings? settings = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingAgentToolExecutor : IAgentToolExecutor
    {
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

        public int CallCount { get; private set; }

        public Task<ToolExecutionResult> ExecuteAsync(
            string? toolName,
            JsonElement arguments,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(ToolExecutionResult.CustomerNotFound());
        }
    }

    private sealed record GuardrailLogEntry(
        DomainScopeDecision Decision,
        DomainScopeReasonCode ReasonCode);
}
