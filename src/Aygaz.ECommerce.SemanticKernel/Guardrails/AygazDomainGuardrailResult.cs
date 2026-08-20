namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

public sealed record AygazDomainGuardrailResult(
    DomainDecision Decision,
    TimeSpan Latency,
    int InferenceCount);
