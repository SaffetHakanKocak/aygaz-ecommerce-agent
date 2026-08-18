namespace Aygaz.ECommerce.Agent.Guardrails;

public sealed record DomainScopeResult(
    DomainScopeDecision Decision,
    DomainScopeReasonCode ReasonCode);
