namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

public interface IAygazDomainGuardrail
{
    Task<AygazDomainGuardrailResult> EvaluateAsync(
        string? userMessage,
        CancellationToken cancellationToken = default);
}
