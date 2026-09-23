namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

public interface IAygazDomainGuardrail
{
    Task<AygazDomainClassificationResult> EvaluateAsync(
        string? userMessage,
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory? conversationHistory = null,
        CancellationToken cancellationToken = default);
}
