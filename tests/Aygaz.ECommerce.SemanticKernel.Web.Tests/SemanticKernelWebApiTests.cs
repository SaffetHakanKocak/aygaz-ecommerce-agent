using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aygaz.ECommerce.SemanticKernel.Capabilities;
using Aygaz.ECommerce.SemanticKernel.Guardrails;
using Aygaz.ECommerce.SemanticKernel.Services;
using Aygaz.ECommerce.SemanticKernel.Web.Models;
using Aygaz.ECommerce.SemanticKernel.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aygaz.ECommerce.SemanticKernel.Web.Tests;

public sealed class SemanticKernelWebApiTests : IClassFixture<SemanticKernelWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly RecordingSemanticKernelChatService _chatService;

    public SemanticKernelWebApiTests(SemanticKernelWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _chatService = factory.ChatService;
        _chatService.Reset();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Chat_EmptyMessage_ReturnsBadRequest(string? message)
    {
        HttpResponseMessage response = await PostChatAsync(new ChatRequest(message, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _chatService.CallCount);
    }

    [Fact]
    public async Task Chat_OversizedMessage_ReturnsBadRequest()
    {
        HttpResponseMessage response = await PostChatAsync(new ChatRequest(new string('a', 2001), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _chatService.CallCount);
    }

    [Fact]
    public async Task Chat_OutOfScope_DoesNotInvokeBusinessAgent()
    {
        _chatService.NextResult = CreateResult(
            DomainGuardedQueryExecutor.OutOfScopeResponse,
            DomainDecision.OutOfScope,
            AygazCapability.Unknown,
            businessAgentInvoked: false);

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "Turkcell müşterilerini getir.",
            null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ChatResponse? payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.Equal(nameof(DomainDecision.OutOfScope), payload.DomainDecision);
        Assert.False(_chatService.LastInvokedBusinessAgent);
        Assert.Equal(1, _chatService.CallCount);
    }

    [Fact]
    public async Task Chat_Ambiguous_DoesNotInvokeBusinessAgent()
    {
        _chatService.NextResult = CreateResult(
            DomainGuardedQueryExecutor.AmbiguousResponse,
            DomainDecision.Ambiguous,
            AygazCapability.Unknown,
            businessAgentInvoked: false);

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "satış rakamları nedir?",
            null));

        ChatResponse? payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.Equal(nameof(DomainDecision.Ambiguous), payload.DomainDecision);
        Assert.False(_chatService.LastInvokedBusinessAgent);
    }

    [Fact]
    public async Task Chat_AllowedCustomerRequest_UsesCustomerPipeline()
    {
        _chatService.NextResult = CreateResult(
            "Ahmet Yılmaz'ın telefon numarası: 0532 000 00 01",
            DomainDecision.Allowed,
            AygazCapability.Customer,
            businessAgentInvoked: true,
            selectedAgent: "CustomerAgent",
            invokedFunction: "search_customers_by_name");

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "Ahmet Yılmaz'ın telefon numarası nedir?",
            null));

        ChatResponse? payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.Equal(nameof(DomainDecision.Allowed), payload.DomainDecision);
        Assert.Equal(nameof(AygazCapability.Customer), payload.Capability);
        Assert.Equal("CustomerAgent", payload.SelectedAgent);
        Assert.True(_chatService.LastInvokedBusinessAgent);
    }

    [Fact]
    public async Task Chat_UnsupportedAllowedCapability_DoesNotInvokeCustomerAgent()
    {
        _chatService.NextResult = CreateResult(
            SemanticKernelChatService.UnsupportedCapabilityResponse,
            DomainDecision.Allowed,
            AygazCapability.ProductInventory,
            businessAgentInvoked: false);

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "AYG-DEMO-PRD-001 stokta mı?",
            null));

        ChatResponse? payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.Equal(nameof(DomainDecision.Allowed), payload.DomainDecision);
        Assert.Equal(nameof(AygazCapability.ProductInventory), payload.Capability);
        Assert.Null(payload.SelectedAgent);
        Assert.False(_chatService.LastInvokedBusinessAgent);
    }

    [Fact]
    public async Task Chat_Response_DoesNotContainSecrets()
    {
        _chatService.NextResult = CreateResult(
            "Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).",
            DomainDecision.Allowed,
            AygazCapability.Customer,
            businessAgentInvoked: true,
            selectedAgent: "CustomerAgent");

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "Ahmet Yılmaz müşterisini bul.",
            null));

        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("gsk_", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chat_Response_DoesNotLeakInternalFunctionNameInMessage()
    {
        _chatService.NextResult = CreateResult(
            "Ahmet Yılmaz'ın telefon numarası: 0532 000 00 01",
            DomainDecision.Allowed,
            AygazCapability.Customer,
            businessAgentInvoked: true,
            selectedAgent: "CustomerAgent",
            invokedFunction: "search_customers_by_name");

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "Ahmet Yılmaz'ın telefon numarası nedir?",
            null));

        ChatResponse? payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.DoesNotContain("search_customers_by_name", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearSession_ReturnsSuccess()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/chat/clear",
            new ClearSessionRequest("test-session"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClearSessionResponse? payload = await response.Content.ReadFromJsonAsync<ClearSessionResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.Equal("test-session", payload.SessionId);
    }

    private Task<HttpResponseMessage> PostChatAsync(ChatRequest request)
    {
        return _client.PostAsJsonAsync("/api/chat", request);
    }

    private static SemanticKernelChatResult CreateResult(
        string message,
        DomainDecision decision,
        AygazCapability capability,
        bool businessAgentInvoked,
        string? selectedAgent = null,
        string? invokedFunction = null)
    {
        return new SemanticKernelChatResult(
            message,
            decision,
            capability,
            selectedAgent,
            DurationMs: 120,
            businessAgentInvoked,
            BusinessFunctionInvoked: invokedFunction is not null,
            GuardrailInferenceCount: 1,
            AgentInferenceCount: businessAgentInvoked ? 1 : null,
            FunctionInvocationCount: invokedFunction is null ? 0 : 1,
            InvokedFunctionName: invokedFunction,
            FastPathUsed: invokedFunction is not null);
    }
}

public sealed class SemanticKernelWebApplicationFactory : WebApplicationFactory<Program>
{
    public RecordingSemanticKernelChatService ChatService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISemanticKernelChatService>();
            services.AddSingleton<ISemanticKernelChatService>(ChatService);
            services.RemoveAll<SemanticKernelAgentHost>();
        });
    }
}

public sealed class RecordingSemanticKernelChatService : ISemanticKernelChatService
{
    public SemanticKernelChatResult? NextResult { get; set; }

    public int CallCount { get; private set; }

    public bool LastInvokedBusinessAgent { get; private set; }

    public void Reset()
    {
        NextResult = null;
        CallCount = 0;
        LastInvokedBusinessAgent = false;
    }

    public Task<SemanticKernelChatResult> ProcessAsync(
        string userMessage,
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        SemanticKernelChatResult result = NextResult ?? new SemanticKernelChatResult(
            "test",
            DomainDecision.Allowed,
            AygazCapability.Customer,
            "CustomerAgent",
            1,
            true,
            false,
            1,
            1,
            0,
            null,
            false);

        LastInvokedBusinessAgent = result.BusinessAgentInvoked;
        return Task.FromResult(result);
    }
}

public sealed class InMemoryChatSessionStoreTests
{
    [Fact]
    public void SameSession_KeepsBoundedHistory()
    {
        var store = new InMemoryChatSessionStore();
        string sessionId = store.ResolveSessionId("s-1");

        for (int i = 0; i < 8; i++)
        {
            store.AppendTurn(sessionId, $"u{i}", $"a{i}");
        }

        var history = store.GetHistory(sessionId);
        Assert.Equal(12, history.Count);
        Assert.Equal("u2", history[0].Content);
        Assert.Equal("a7", history[^1].Content);
    }

    [Fact]
    public void Clear_RemovesOnlyTargetSession()
    {
        var store = new InMemoryChatSessionStore();
        string a = store.ResolveSessionId("a");
        string b = store.ResolveSessionId("b");
        store.AppendTurn(a, "ua", "aa");
        store.AppendTurn(b, "ub", "ab");

        store.ClearSession(a);

        Assert.Empty(store.GetHistory(a));
        Assert.Equal(2, store.GetHistory(b).Count);
    }

    [Fact]
    public void SeparateSessions_DoNotShareCustomerContext()
    {
        var store = new InMemoryChatSessionStore();
        string sessionA = store.ResolveSessionId("session-a");
        string sessionB = store.ResolveSessionId("session-b");

        store.AppendTurn(sessionA, "Ahmet Yılmaz'ın bilgileri", "Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");
        store.AppendTurn(sessionB, "Mehmet Kaya'nın bilgileri", "Müşteri: Mehmet Kaya (ID: 2, Ankara).");

        var historyA = store.GetHistory(sessionA);
        var historyB = store.GetHistory(sessionB);

        Assert.Contains("Ahmet", historyA[^1].Content!, StringComparison.Ordinal);
        Assert.DoesNotContain("Mehmet", historyA[^1].Content!, StringComparison.Ordinal);
        Assert.Contains("Mehmet", historyB[^1].Content!, StringComparison.Ordinal);
        Assert.DoesNotContain("Ahmet", historyB[^1].Content!, StringComparison.Ordinal);

        store.ClearSession(sessionA);
        Assert.Empty(store.GetHistory(sessionA));
        Assert.Equal(2, store.GetHistory(sessionB).Count);
    }
}
