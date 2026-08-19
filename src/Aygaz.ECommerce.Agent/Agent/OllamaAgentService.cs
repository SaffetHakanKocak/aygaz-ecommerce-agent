using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Agent;

public sealed class OllamaAgentService : IAgentService
{
    private const string SystemPrompt =
        "Sen Aygaz sentetik e-ticaret geliştirme verileri için read-only bir asistansın. " +
        "ZORUNLU NATIVE LOOP: Orijinal istek sipariş soruyorsa customer tool sonucundan sonra " +
        "order tool çağırmadan final cevap verme. Orijinal istek stok/inventory soruyorsa " +
        "product tool sonucundan sonra inventory tool çağırmadan final cevap verme. " +
        "Yalnız kullanıcının açıkça istediği veri türünü sorgula; tool sonuçları dışında " +
        "müşteri, sipariş, ürün, stok veya satış bilgisi üretme. " +
        "Müşteri kimliği/bilgisi isteğinde yalnız customer tool kullan ve sipariş sorulmadıysa " +
        "order tool çağırma. Sipariş isteğinde customer ID bilinmiyorsa önce uygun customer " +
        "lookup tool'unu, ardından aynı istek içinde get_customer_orders veya " +
        "get_latest_customer_order tool'unu çağır; ara plan anlatma ve customer sonucundan " +
        "sipariş verisi çıkarma. " +
        "Satış özeti için yalnız get_sales_summary, en çok satan ürünler için yalnız " +
        "get_top_selling_products kullan. Müşteri satış özeti isteğinde customer ID " +
        "bilinmiyorsa önce uygun customer lookup tool'unu, ardından aynı istek içinde " +
        "get_customer_purchase_summary tool'unu çağır ve arada final cevap verme. Göreli " +
        "satış tarihlerini sales tool açıklamasındaki güvenilir demo referans tarihine göre " +
        "exact ISO tarihlere çevir. Ham sipariş veya order-item verisini toplama; sales tool " +
        "aggregate sonucunu değiştirmeden kullan, kendin hesap yapma. İptal edilen siparişlerin " +
        "hariç tutulduğunu kabul et ve para birimi olarak yalnız sonuçtaki currencyCode'u kullan. " +
        "Yalnız ürün kimliği/detayı isteniyorsa stok sorgulama: AYG-DEMO-PRD-001 gibi tam " +
        "SKU içeren her istekte yalnız get_product_by_sku; ad veya kategori isteğinde " +
        "search_products kullan ve ürün sonucuyla " +
        "cevap ver. Stok/inventory açıkça istenmişse önce ürünü bul; tek ürünün data.id " +
        "değeriyle aynı istek içinde stokta olma/toplam için get_total_product_stock, " +
        "lokasyon ayrıntısı için get_product_inventory çağır. Product sonucu stok kanıtı " +
        "değildir. Birden fazla eşleşmede rastgele seçim yapma, netleştirme iste. " +
        "Inventory NotFound ise miktar uydurma; toplam 0 ise stokta olmadığını söyle. Inactive " +
        "ürün durumunu gizleme. " +
        "Create/update/delete/cancel/refund, fiyat veya stok değiştirme isteklerinde hiçbir " +
        "tool çağırma; write yeteneğinin olmadığını açıkça söyle. Tüm müşteri, sipariş, ürün, " +
        "stok, satış veya order-item verisini dökme; bulk işlem yapma. Bulunamadı deme ancak " +
        "lookup NotFound döndüyse. " +
        "Politika/prosedür (iade, teslimat, kampanya, müşteri destek) sorularında kendi " +
        "hafızandan bilgi üretme; yalnız search_documents kullan. search_documents NotFound " +
        "döndürürse bilgi uydurma; mevcut demo dokümanlarda bilgi bulunamadığını söyle. " +
        "Doküman tool sonucundaki metne dayanarak kısa Türkçe cevap ver. " +
        "Tool sonucunda olmayan telefon, ödeme, adres veya alanları uydurma. TotalAmount ve " +
        "UnitPrice para birimsiz sentetik sayılardır; kullanıcı açıkça istemedikçe gösterme " +
        "ve hiçbir para birimi ekleme. Sales tool para alanlarında yalnız tool'un döndürdüğü " +
        "currencyCode'u kullan. Selamlaşmada tool kullanma. Kısa, açık Türkçe cevap ver.";

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
