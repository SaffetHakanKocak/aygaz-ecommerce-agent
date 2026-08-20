#pragma warning disable SKEXP0070

using System.Diagnostics;
using System.Text.Json;
using Aygaz.AgentFramework.Configuration;
using Aygaz.ECommerce.SemanticKernel.Capabilities;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

public sealed class AygazDomainGuardrail : IAygazDomainGuardrail
{
    public const int MaxInputCharacters = 4000;

    private const string SystemPrompt =
        """
        Sen Aygaz e-ticaret uygulaması için domain+capability sınıflandırıcısısın.
        Kullanıcıya cevap üretme, tool/function çağırma, açıklama yazma.

        Görev:
        1) Decision ver: Allowed | OutOfScope | Ambiguous
        2) Capability ver: Customer | Order | ProductInventory | Sales | Policy | Unknown

        Kurallar:
        - Aygaz müşteri/sipariş/ürün-stok/satış/politika destek soruları Allowed.
        - "Ahmet Yılmaz'ın bilgileri", "1 numaralı müşteri", "İstanbul'daki müşteriler" gibi uygulama içi müşteri sorguları Aygaz adı yazmasa da Allowed + Customer.
        - Current message may contain pronouns or omitted customer identity. Use recent conversation context to resolve references such as "onun", "telefonu", "adresi", "bilgileri", "tüm bilgilerini". If the recent conversation clearly identifies an Aygaz customer, classify the follow-up as Allowed + Customer.
        - "Aygazın cirosu ne kadar?" Allowed + Sales.
        - "AYG-DEMO-PRD-001 stokta mı?" Allowed + ProductInventory.
        - "Aygaz iade politikası nedir?" Allowed + Policy.
        - Başka şirket açıkça geçiyorsa (örn Turkcell) OutOfScope + Unknown.
        - Genel dünya bilgisi, spor, coğrafya vb. OutOfScope + Unknown.
        - Aygaz ile ilgili ama eksik/bağlamsız sorular Ambiguous olabilir; capability tahmini yapılabiliyorsa doldur.

        Çıktı yalnızca geçerli JSON olsun:
        {"decision":"Allowed|OutOfScope|Ambiguous","capability":"Customer|Order|ProductInventory|Sales|Policy|Unknown","reason":"kısa opsiyonel neden"}
        """;

    private readonly Microsoft.SemanticKernel.Kernel _kernel;
    private readonly SemanticKernelOptions _options;

    public AygazDomainGuardrail(Microsoft.SemanticKernel.Kernel kernel, SemanticKernelOptions options)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(options);
        _kernel = kernel;
        _options = options;
    }

    public async Task<AygazDomainClassificationResult> EvaluateAsync(
        string? userMessage,
        ChatHistory? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(userMessage)
            || userMessage.Trim().Length > MaxInputCharacters)
        {
            sw.Stop();
            return new AygazDomainClassificationResult(
                DomainDecision.Ambiguous,
                AygazCapability.Unknown,
                "empty_or_oversized_input",
                sw.Elapsed,
                0);
        }

        var history = new ChatHistory();
        history.AddSystemMessage(SystemPrompt);
        history.AddUserMessage(JsonSerializer.Serialize(new
        {
            request = userMessage.Trim(),
            recentConversation = BuildRecentConversation(conversationHistory)
        }));

        try
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            ChatMessageContent response = await chat.GetChatMessageContentAsync(
                history,
                CreateExecutionSettings(),
                _kernel,
                cancellationToken);

            sw.Stop();
            bool parsed = DomainDecisionParser.TryParse(
                response.Content,
                out DomainDecision decision,
                out AygazCapability capability,
                out string? reason);

            return parsed
                ? new AygazDomainClassificationResult(decision, capability, reason, sw.Elapsed, 1)
                : new AygazDomainClassificationResult(
                    DomainDecision.Ambiguous,
                    AygazCapability.Unknown,
                    "parse_failed",
                    sw.Elapsed,
                    1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            sw.Stop();
            return new AygazDomainClassificationResult(
                DomainDecision.Ambiguous,
                AygazCapability.Unknown,
                "guardrail_exception",
                sw.Elapsed,
                1);
        }
    }

    private static IReadOnlyList<object> BuildRecentConversation(ChatHistory? conversationHistory)
    {
        if (conversationHistory is null || conversationHistory.Count == 0)
        {
            return [];
        }

        return conversationHistory
            .TakeLast(12)
            .Where(message =>
                message.Role == AuthorRole.User
                || message.Role == AuthorRole.Assistant)
            .Select(message => new
            {
                role = message.Role == AuthorRole.User ? "user" : "assistant",
                content = message.Content
            })
            .ToArray();
    }

    private PromptExecutionSettings CreateExecutionSettings()
    {
        return _options.Provider switch
        {
            SemanticKernelProvider.OpenAI or SemanticKernelProvider.Groq => new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
                Temperature = 0
            },
            _ => new OllamaPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
                Temperature = 0
            }
        };
    }
}
