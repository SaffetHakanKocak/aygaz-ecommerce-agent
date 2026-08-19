using Aygaz.ECommerce.Agent.Guardrails;

namespace Aygaz.ECommerce.Web;

public sealed class NullDomainGuardrailLogger : IDomainGuardrailLogger
{
    public void LogDecision(
        DomainScopeDecision decision,
        DomainScopeReasonCode reasonCode)
    {
    }
}
