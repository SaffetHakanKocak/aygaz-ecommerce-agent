#pragma warning disable SKEXP0070

using System.Diagnostics;
using System.Text.Json;
using Aygaz.AgentFramework.Configuration;
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
        Sen yalnızca Aygaz e-ticaret asistanı için domain kapsamı sınıflandırıcısısın.
        Kullanıcıya iş cevabı verme. Tool/function çağırma. Açıklama yazma.

        Uygulama bağlamı Aygaz e-ticarettir.

        Allowed:
        - Aygaz müşteri, sipariş, ürün/stok, iade, teslimat, kampanya veya destek talepleri.
        - Organizasyon adı yazılmasa bile müşteri adı/id/e-posta, sipariş veya ürün/SKU araması.
        - Müşteri telefon numarası veya adres bilgisi talepleri Allowed.
        - "siparişim nerede?" gibi Aygaz uygulaması bağlamındaki işlemler.
        - Aygaz politika/prosedür soruları.
        - Henüz bu asistanın yeteneği olmasa bile Aygaz e-ticaret operasyonu Allowed kalır.

        OutOfScope:
        - Başka bir şirkete/organizasyona ait bilgi, müşteri, stok, satış veya politika.
        - Aygaz e-ticaret ile ilgisiz genel bilgi (coğrafya, programlama, hava, spor vb.).

        Ambiguous:
        - Aygaz ile ilgili olup olmadığı anlaşılamayan kısa/belirsiz istekler.
        - Örnek: hedef organizasyon veya işlem belirtilmeden "satış rakamları nedir?"

        Yalnızca şu JSON'u döndür: {"decision":"Allowed"} veya {"decision":"OutOfScope"} veya {"decision":"Ambiguous"}
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

    public async Task<AygazDomainGuardrailResult> EvaluateAsync(
        string? userMessage,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(userMessage)
            || userMessage.Trim().Length > MaxInputCharacters)
        {
            sw.Stop();
            return new AygazDomainGuardrailResult(DomainDecision.Ambiguous, sw.Elapsed, 0);
        }

        var history = new ChatHistory();
        history.AddSystemMessage(SystemPrompt);
        history.AddUserMessage(JsonSerializer.Serialize(new { request = userMessage.Trim() }));

        try
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            ChatMessageContent response = await chat.GetChatMessageContentAsync(
                history,
                CreateExecutionSettings(),
                _kernel,
                cancellationToken);

            sw.Stop();
            DomainDecision decision = DomainDecisionParser.TryParse(response.Content, out DomainDecision parsed)
                ? parsed
                : DomainDecision.Ambiguous;

            return new AygazDomainGuardrailResult(decision, sw.Elapsed, 1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            sw.Stop();
            return new AygazDomainGuardrailResult(DomainDecision.Ambiguous, sw.Elapsed, 1);
        }
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
