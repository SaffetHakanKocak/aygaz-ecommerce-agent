using Aygaz.ECommerce.SemanticKernel.Capabilities;

namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

public sealed record AygazDomainClassificationResult(
    DomainDecision Decision,
    AygazCapability Capability,
    string? Reason,
    TimeSpan Latency,
    int InferenceCount);
