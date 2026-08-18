namespace Aygaz.ECommerce.Agent.Guardrails;

public enum DomainScopeReasonCode
{
    ClassifierResponseInvalid = 0,
    ClassifiedAllowed,
    ClassifiedOutOfScope,
    ClassifiedAmbiguous,
    ClassifierUnavailable,
    InvalidInput
}
