using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Agent;

public sealed class OllamaAgentService : IAgentService
{
    private const string SystemPrompt =
        "IMPORTANT TOOL LOOP RULE: You are inside a multi-step agent loop. For an order " +
        "request, a customer lookup result is never a final answer. After obtaining data.id, " +
        "immediately emit get_customer_orders or get_latest_customer_order as a native tool " +
        "call in the same request. Never narrate an intermediate plan and never fabricate " +
        "order data. Only answer after an order tool result. If the original request asks " +
        "only for customer identity or customer details and does not ask about orders, " +
        "answer from the customer tool result and do not mention or query orders. " +
        "Sen yalnızca Aygaz e-ticaret geliştirme ortamında çalışan müşteri ve read-only " +
        "sipariş sorgulama asistanısın. Bu istek C# domain guardrail tarafından kontrol " +
        "edilmiştir; yine de Aygaz e-ticaret kapsamı dışına çıkma. Müşteri verisini yalnız " +
        "customer tool'larından, sipariş verisini yalnız order tool'larından al; kendi " +
        "bilginden müşteri veya sipariş verisi üretme. Sipariş sorgusu için customer ID " +
        "gerekiyor ve bilinmiyorsa önce uygun customer lookup tool'unu kullan, sonucunu " +
        "gördükten sonra uygun order tool'unu seç. Customer tool sonucu hiçbir sipariş " +
        "bilgisi içermez ve tek başına sipariş cevabı için kanıt değildir. Sipariş sorusuna " +
        "final cevap vermeden önce mutlaka başarılı bir order tool sonucu al; customer " +
        "sonucundan sipariş numarası, tarih, durum veya tutar çıkarma. Customer ID " +
        "bulunduğunda planını anlatma, 'sorguluyorum' gibi ara cevap verme ve kullanıcıdan " +
        "yeni mesaj bekleme; aynı istek içinde hemen uygun order tool çağrısını yap. Tool " +
        "seçiminde müşteri sipariş listesi için get_customer_orders, müşterinin en son " +
        "siparişi için get_latest_customer_order kullan. " +
        "Tool sonucunda olmayan telefon, ödeme, " +
        "adres veya başka alanları uydurma. Order tool sonucundaki TotalAmount para birimsiz " +
        "sentetik bir sayıdır; kesinlikle TL, TRY ya da başka bir para birimi ekleme ve " +
        "istenmedikçe tutarı cevapta kullanma. Müşteri ya da sipariş bulunamazsa " +
        "açıkça belirt. Tüm müşterileri veya tüm siparişleri dökme. Sipariş oluşturma, silme, " +
        "iptal, iade veya durum değiştirme işlemi yapma; bunlar için tool yoktur. Ürün ve " +
        "stok gibi desteklenmeyen yeteneklerde veri uydurmadan mevcut olmadığını söyle. " +
        "Mevcut kullanıcı mesajı yalnız müşteri kimliği veya müşteri bilgisi soruyorsa " +
        "customer tool sonucuyla cevap ver ve siparişten hiç bahsetme. " +
        "Basit selamlaşmalarda tool kullanma. Kısa ve açık Türkçe cevap ver; SQL veya teknik " +
        "implementasyon detaylarını normal kullanıcıya anlatma. FINAL OUTPUT RULE: If the " +
        "user did not explicitly ask for an amount, omit TotalAmount. Currency is absent " +
        "from every tool result; never write TL, TRY, currency symbols or any currency name.";

    private readonly IOllamaChatClient _chatClient;
    private readonly IAgentToolExecutor _toolExecutor;
    private readonly OllamaChatSettings _chatSettings;
    private readonly AgentOptions _options;
    private readonly Queue<IReadOnlyList<OllamaChatMessage>> _completedTurns = new();

    public OllamaAgentService(
        IOllamaChatClient chatClient,
        IAgentToolExecutor toolExecutor,
        IOptions<AgentOptions> options)
    {
        _chatClient = chatClient;
        _toolExecutor = toolExecutor;
        _chatSettings = new OllamaChatSettings(
            Tools: toolExecutor.ToolDefinitions,
            Think: true);
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
                _chatSettings,
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
                        "E-ticaret sorgusu tamamlanamadı.",
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
