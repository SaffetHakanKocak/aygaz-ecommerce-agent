namespace Aygaz.ECommerce.Agent.Guardrails;

public interface IDomainGuardrailService
{
    Task<DomainScopeResult> EvaluateAsync(
        string? userInput,
        CancellationToken cancellationToken = default);
}
