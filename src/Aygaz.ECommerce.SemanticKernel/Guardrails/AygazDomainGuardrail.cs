#pragma warning disable SKEXP0070

using System.Diagnostics;
using System.Text.Json;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Resilience;
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

        Görev (sırayla):
        1) First decide if the request is about the Aygaz domain.
        2) Then decide capability: Customer | Order | ProductInventory | Sales | Policy | Unknown
        3) Decision: Allowed | OutOfScope | Ambiguous

        Domain vs capability:
        - Being about Aygaz and having a supported capability are different.
        - Explicitly Aygaz-related requests with no matching business capability are Allowed + Unknown.
        - Never mark an explicitly Aygaz-related request as OutOfScope only because its capability is unsupported.
        - Never route generic Aygaz company-information questions to Customer.

        Examples Allowed + Unknown:
        - "Aygaz CEO kim", "Aygaz'ın CEO'su kim"
        - "Aygaz ne zaman kuruldu"
        - "Aygaz genel merkezi nerede"
        - "Aygaz hakkında bilgi verir misin"
        - "Aygaz çalışan sayısı kaç"

        Other Allowed examples:
        - Customer: "Ahmet Yılmaz'ın bilgileri", "1 numaralı müşteri", "İstanbul'daki müşteriler" (Aygaz adı olmasa da)
        - Order: "AYG-DEMO-1004 siparişinin durumu", "1 numaralı müşterinin siparişleri", "1 numaralı müşterinin son siparişi"
        - Sales: "Aygazın cirosu ne kadar?"
        - ProductInventory: "AYG-DEMO-PRD-001 stokta mı?"
        - Policy: "Aygaz iade politikası nedir?"

        History rule:
        - Current explicit intent in the user message takes precedence over previous conversation context.
        - Use recent conversation history ONLY for referential follow-ups such as "durumu neydi", "telefonu neydi", "tüm bilgilerini getir", "bu müşterinin siparişleri".
        - Do not use customer history to classify "Aygaz'ın CEO'su kim?" as Customer.

        OutOfScope (Aygaz dışı):
        - Other companies: Turkcell, Trendyol, Amazon, Arçelik
        - General world knowledge / sports / geography: "BJK maçı", "Türkiye'nin başkenti"

        Ambiguous: unclear Aygaz-related asks without enough signal; fill capability when estimable.

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
        catch (AiProviderTemporarilyUnavailableException)
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
