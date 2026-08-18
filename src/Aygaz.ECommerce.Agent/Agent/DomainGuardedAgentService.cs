using Aygaz.ECommerce.Agent.Guardrails;

namespace Aygaz.ECommerce.Agent.Agent;

public sealed class DomainGuardedAgentService : IGuardedAgentService
{
    public const string OutOfScopeResponse =
        "Bu asistan yalnızca Aygaz e-ticaret kapsamındaki işlemler için kullanılabilir.";

    public const string AmbiguousResponse =
        "Bunu Aygaz e-ticaret kapsamında hangi işlem için sorduğunuzu biraz daha " +
        "netleştirebilir misiniz?";

    private readonly IDomainGuardrailService _guardrailService;
    private readonly IAgentService _agentService;
    private readonly IDomainGuardrailLogger _logger;

    public DomainGuardedAgentService(
        IDomainGuardrailService guardrailService,
        IAgentService agentService,
        IDomainGuardrailLogger logger)
    {
        _guardrailService = guardrailService;
        _agentService = agentService;
        _logger = logger;
    }

    public async Task<string> AskAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        string normalizedMessage = userMessage.Trim();
        DomainScopeResult? scopeResult;

        try
        {
            scopeResult = await _guardrailService.EvaluateAsync(
                normalizedMessage,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            scopeResult = new DomainScopeResult(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifierUnavailable);
        }

        scopeResult = NormalizeFailClosed(scopeResult);
        _logger.LogDecision(scopeResult.Decision, scopeResult.ReasonCode);

        return scopeResult.Decision switch
        {
            DomainScopeDecision.Allowed =>
                await _agentService.AskAsync(normalizedMessage, cancellationToken),
            DomainScopeDecision.OutOfScope => OutOfScopeResponse,
            _ => AmbiguousResponse
        };
    }

    private static DomainScopeResult NormalizeFailClosed(DomainScopeResult? result)
    {
        if (result is null)
        {
            return new DomainScopeResult(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifierResponseInvalid);
        }

        bool isConsistent = result.Decision switch
        {
            DomainScopeDecision.Allowed =>
                result.ReasonCode == DomainScopeReasonCode.ClassifiedAllowed,
            DomainScopeDecision.OutOfScope =>
                result.ReasonCode == DomainScopeReasonCode.ClassifiedOutOfScope,
            DomainScopeDecision.Ambiguous => result.ReasonCode is
                DomainScopeReasonCode.ClassifiedAmbiguous
                or DomainScopeReasonCode.ClassifierResponseInvalid
                or DomainScopeReasonCode.ClassifierUnavailable
                or DomainScopeReasonCode.InvalidInput,
            _ => false
        };

        return isConsistent
            ? result
            : new DomainScopeResult(
                DomainScopeDecision.Ambiguous,
                DomainScopeReasonCode.ClassifierResponseInvalid);
    }
}
