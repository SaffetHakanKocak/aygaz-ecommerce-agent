namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

public sealed record DomainGuardedQueryResult(
    AygazDomainGuardrailResult Guardrail,
    string Content,
    bool BusinessAgentInvoked,
    bool BusinessFunctionInvoked,
    TimeSpan? AgentDuration = null);
