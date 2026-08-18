using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Agent;

public sealed class OllamaAgentService : IAgentService
{
    private const string SystemPrompt =
        "Sen Aygaz E-Commerce AI Agent'ın geliştirme ortamındaki müşteri sorgulama " +
        "asistanısın. Müşteriyle ilgili gerçek verilere kendi bilginden cevap verme. " +
        "Müşteri bilgisi sorulduğunda yalnızca sana sağlanan customer tool'larını kullan. " +
        "Tool sonucunda olmayan bilgiyi, özellikle telefon bilgisini, uydurma. Müşteri " +
        "bulunamazsa açıkça müşteri bulunamadığını söyle. Birden fazla müşteri bulunursa " +
        "sonuçları kısa biçimde göster veya gerekirse ayırt edici bilgi iste. Sana toplu " +
        "müşteri listeleme aracı verilmemiştir; tüm müşteri tablosunu sunduğunu iddia etme. " +
        "Basit selamlaşmalarda tool kullanma. Kısa ve açık Türkçe cevaplar ver. Database, " +
        "SQL veya teknik implementasyon detaylarını normal kullanıcıya anlatma.";

    private readonly IOllamaChatClient _chatClient;
    private readonly IAgentToolExecutor _toolExecutor;
    private readonly AgentOptions _options;
    private readonly Queue<IReadOnlyList<OllamaChatMessage>> _completedTurns = new();

    public OllamaAgentService(
        IOllamaChatClient chatClient,
        IAgentToolExecutor toolExecutor,
        IOptions<AgentOptions> options)
    {
        _chatClient = chatClient;
        _toolExecutor = toolExecutor;
        _options = options.Value;
    }

    public async Task<string> AskAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        string normalizedMessage = userMessage.Trim();
        var messages = BuildHistory(normalizedMessage);
        int toolIterations = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            OllamaChatMessage assistantMessage = await _chatClient.ChatAsync(
                messages,
                _toolExecutor.ToolDefinitions,
                cancellationToken);

            if (!string.Equals(
                    assistantMessage.Role,
                    "assistant",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new AgentException(
                    "Agent geçerli bir yanıt üretemedi.",
                    $"Beklenen assistant rolü yerine '{assistantMessage.Role}' alındı.");
            }

            messages.Add(assistantMessage);

            IReadOnlyList<OllamaToolCall> toolCalls =
                assistantMessage.ToolCalls ?? Array.Empty<OllamaToolCall>();

            if (toolCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                {
                    throw new AgentException(
                        "Agent geçerli bir yanıt üretemedi.",
                        "Assistant mesajında content ve tool_calls alanları boş.");
                }

                string finalAnswer = assistantMessage.Content.Trim();
                RememberCompletedTurn(normalizedMessage, finalAnswer);
                return finalAnswer;
            }

            if (toolIterations >= _options.MaxToolIterations)
            {
                throw new AgentException(
                    "İstek güvenli işlem sınırları içinde tamamlanamadı.",
                    $"MaxToolIterations sınırı aşıldı: {_options.MaxToolIterations}.");
            }

            if (toolCalls.Count > _options.MaxToolCallsPerIteration)
            {
                throw new AgentException(
                    "İstek güvenli işlem sınırları içinde tamamlanamadı.",
                    "Tek bir model yanıtındaki tool çağrısı güvenli sınırı aştı.");
            }

            toolIterations++;

            foreach (OllamaToolCall? toolCall in toolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (toolCall?.Function is null
                    || string.IsNullOrWhiteSpace(toolCall.Function.Name)
                    || toolCall.Function.Arguments.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                {
                    throw new AgentException(
                        "Agent geçerli bir tool çağrısı üretemedi.",
                        "Tool call içindeki function, name veya arguments alanı eksik.");
                }

                string toolName = toolCall.Function.Name;
                ToolExecutionResult executionResult;

                try
                {
                    executionResult = await _toolExecutor.ExecuteAsync(
                        toolName,
                        toolCall.Function.Arguments,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new AgentException(
                        "Müşteri sorgusu tamamlanamadı.",
                        $"Allow-list tool yürütme hatası: {exception.GetType().Name}.",
                        exception);
                }

                messages.Add(new OllamaChatMessage(
                    "tool",
                    executionResult.Content,
                    ToolName: toolName,
                    ToolCallId: toolCall.Id));
            }
        }
    }

    private List<OllamaChatMessage> BuildHistory(string userMessage)
    {
        var messages = new List<OllamaChatMessage>
        {
            new("system", SystemPrompt)
        };

        foreach (IReadOnlyList<OllamaChatMessage> completedTurn in _completedTurns)
        {
            messages.AddRange(completedTurn);
        }

        messages.Add(new OllamaChatMessage("user", userMessage));
        return messages;
    }

    private void RememberCompletedTurn(string userMessage, string assistantMessage)
    {
        _completedTurns.Enqueue(
        [
            new OllamaChatMessage("user", userMessage),
            new OllamaChatMessage("assistant", assistantMessage)
        ]);

        while (_completedTurns.Count > _options.MaxConversationTurns)
        {
            _completedTurns.Dequeue();
        }
    }
}
