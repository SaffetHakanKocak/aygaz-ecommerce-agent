using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Agent;
using Aygaz.ECommerce.Agent.Guardrails;
using Aygaz.ECommerce.Web;
using Aygaz.ECommerce.Web.Models;
using Aygaz.ECommerce.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class WebApiTests : IClassFixture<AygazWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly RecordingGuardedAgentService _guardedAgent;

    public WebApiTests(AygazWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _guardedAgent = factory.GuardedAgent;
        _guardedAgent.Reset();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Chat_ValidRequest_ReturnsControlledResponseWithoutRawPayloads()
    {
        _guardedAgent.NextResponse = new GuardedAgentResponse(
            "Demo Product Alpha stokta.",
            nameof(DomainScopeDecision.Allowed),
            Success: true);

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "AYG-DEMO-PRD-001 stokta mı?",
            null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("tool_calls", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CustomerDto", body, StringComparison.Ordinal);
        Assert.DoesNotContain("embedding", body, StringComparison.OrdinalIgnoreCase);

        ChatResponse? payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.Equal("Demo Product Alpha stokta.", payload.Message);
        Assert.Equal(nameof(DomainScopeDecision.Allowed), payload.Scope);
        Assert.False(string.IsNullOrWhiteSpace(payload.SessionId));
        Assert.Equal(1, _guardedAgent.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Chat_EmptyMessage_ReturnsBadRequest(string? message)
    {
        HttpResponseMessage response = await PostChatAsync(new ChatRequest(message, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiErrorResponse? payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(0, _guardedAgent.CallCount);
    }

    [Fact]
    public async Task Chat_OversizedMessage_ReturnsBadRequest()
    {
        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            new string('a', 1001),
            null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _guardedAgent.CallCount);
    }

    [Fact]
    public async Task Chat_OutOfScopeQuery_ReturnsControlledResponseWithoutCallingAgent()
    {
        _guardedAgent.NextResponse = new GuardedAgentResponse(
            DomainGuardedAgentService.OutOfScopeResponse,
            nameof(DomainScopeDecision.OutOfScope),
            Success: true);

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "Arçelik'in satışlarını göster.",
            null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ChatResponse? payload = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(payload);
        Assert.Equal(nameof(DomainScopeDecision.OutOfScope), payload.Scope);
        Assert.Equal(DomainGuardedAgentService.OutOfScopeResponse, payload.Message);
        Assert.Equal(1, _guardedAgent.CallCount);
    }

    [Fact]
    public async Task Chat_AgentFailure_ReturnsSafeServiceUnavailableResponse()
    {
        _guardedAgent.Exception = new AgentException(
            "E-ticaret sorgusu tamamlanamadı.",
            "test failure");

        HttpResponseMessage response = await PostChatAsync(new ChatRequest(
            "AYG-DEMO-PRD-001 stokta mı?",
            null));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        ApiErrorResponse? payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("E-ticaret sorgusu tamamlanamadı.", payload.Message);
        Assert.DoesNotContain("test failure", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_ReusesSessionIdAcrossRequests()
    {
        _guardedAgent.Responses.Enqueue(new GuardedAgentResponse(
            "Ahmet Yılmaz bulundu.",
            nameof(DomainScopeDecision.Allowed),
            Success: true));
        _guardedAgent.Responses.Enqueue(new GuardedAgentResponse(
            "Son sipariş hazırlanıyor.",
            nameof(DomainScopeDecision.Allowed),
            Success: true));

        ChatResponse? first = await PostChatAndReadAsync(new ChatRequest(
            "Ahmet Yılmaz'ı bul.",
            null));
        ChatResponse? second = await PostChatAndReadAsync(new ChatRequest(
            "Son siparişi ne durumda?",
            first!.SessionId));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(2, _guardedAgent.CallCount);
    }

    private async Task<HttpResponseMessage> PostChatAsync(ChatRequest request)
    {
        return await _client.PostAsJsonAsync("/api/chat", request);
    }

    private async Task<ChatResponse?> PostChatAndReadAsync(ChatRequest request)
    {
        HttpResponseMessage response = await PostChatAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponse>();
    }
}

public sealed class AygazWebApplicationFactory : WebApplicationFactory<Program>
{
    public RecordingGuardedAgentService GuardedAgent { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatSessionStore>();
            services.AddSingleton<IChatSessionStore>(
                _ => new StubChatSessionStore(GuardedAgent));
        });
    }
}

public sealed class RecordingGuardedAgentService : IGuardedAgentService
{
    public int CallCount { get; private set; }

    public Queue<GuardedAgentResponse> Responses { get; } = new();

    public GuardedAgentResponse? NextResponse { get; set; }

    public Exception? Exception { get; set; }

    public void Reset()
    {
        CallCount = 0;
        Responses.Clear();
        NextResponse = null;
        Exception = null;
    }

    public async Task<string> AskAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        GuardedAgentResponse response = await AskDetailedAsync(userMessage, cancellationToken);
        return response.Message;
    }

    public Task<GuardedAgentResponse> AskDetailedAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;

        if (Exception is not null)
        {
            return Task.FromException<GuardedAgentResponse>(Exception);
        }

        GuardedAgentResponse response = Responses.Count > 0
            ? Responses.Dequeue()
            : NextResponse
                ?? new GuardedAgentResponse(
                    "Test yanıtı",
                    nameof(DomainScopeDecision.Allowed),
                    Success: true);

        return Task.FromResult(response);
    }
}

internal sealed class StubChatSessionStore(IGuardedAgentService guardedAgent) : IChatSessionStore
{
    public Task<ChatSessionHandle> GetOrCreateAsync(
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string resolvedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString("N")
            : sessionId.Trim();

        return Task.FromResult(new ChatSessionHandle(resolvedSessionId, guardedAgent));
    }

    public bool ClearSession(string sessionId)
    {
        return !string.IsNullOrWhiteSpace(sessionId);
    }
}
