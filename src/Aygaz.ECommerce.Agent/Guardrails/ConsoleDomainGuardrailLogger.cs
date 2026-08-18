namespace Aygaz.ECommerce.Agent.Guardrails;

public sealed class ConsoleDomainGuardrailLogger : IDomainGuardrailLogger
{
    public void LogDecision(
        DomainScopeDecision decision,
        DomainScopeReasonCode reasonCode)
    {
        Console.WriteLine($"[Guardrail] {decision} | {reasonCode}");
    }
}
