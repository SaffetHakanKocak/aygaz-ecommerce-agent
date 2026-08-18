namespace Aygaz.ECommerce.Agent.Guardrails;

public interface IDomainGuardrailLogger
{
    void LogDecision(
        DomainScopeDecision decision,
        DomainScopeReasonCode reasonCode);
}
