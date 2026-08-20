using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Observability;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.SemanticKernel.Agents;

namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

public sealed class DomainGuardedQueryExecutor
{
    public const string OutOfScopeResponse =
        "Bu asistan yalnızca Aygaz ile ilgili konularda yardımcı olabilir.";

    public const string AmbiguousResponse =
        "Sorunuzu Aygaz ile ilgili olacak şekilde biraz daha netleştirebilir misiniz?";

    private readonly IAygazDomainGuardrail _guardrail;
    private readonly IAgentRouter _router;
    private readonly IAgentRegistry _registry;
    private readonly IAgentRunner _runner;
    private readonly KernelInvocationTelemetry? _telemetry;
    private readonly string _routeKey;

    public DomainGuardedQueryExecutor(
        IAygazDomainGuardrail guardrail,
        IAgentRouter router,
        IAgentRegistry registry,
        IAgentRunner runner,
        KernelInvocationTelemetry? telemetry = null,
        string? routeKey = null)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(runner);

        _guardrail = guardrail;
        _router = router;
        _registry = registry;
        _runner = runner;
        _telemetry = telemetry;
        _routeKey = string.IsNullOrWhiteSpace(routeKey)
            ? CustomerAgentRegistration.RouteKey
            : routeKey;
    }

    public async Task<DomainGuardedQueryResult> ExecuteAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        AygazDomainClassificationResult guardrail = await _guardrail.EvaluateAsync(
            userMessage,
            conversationHistory: null,
            cancellationToken);

        if (guardrail.Decision == DomainDecision.OutOfScope)
        {
            return Blocked(guardrail, OutOfScopeResponse);
        }

        if (guardrail.Decision != DomainDecision.Allowed)
        {
            return Blocked(guardrail, AmbiguousResponse);
        }

        string? agentName = _router.ResolveAgentName(_routeKey);
        if (agentName is null)
        {
            throw new InvalidOperationException($"No agent is mapped for route '{_routeKey}'.");
        }

        _telemetry?.Reset();
        AgentResponse response = await _runner.InvokeAsync(
            _registry.GetAgent(agentName),
            userMessage.Trim(),
            conversationHistory: null,
            cancellationToken);

        return new DomainGuardedQueryResult(
            guardrail,
            response.Content,
            BusinessAgentInvoked: true,
            BusinessFunctionInvoked: (_telemetry?.FunctionInvocationCount ?? 0) > 0,
            AgentDuration: response.Duration);
    }

    private static DomainGuardedQueryResult Blocked(
        AygazDomainClassificationResult guardrail,
        string content)
    {
        return new DomainGuardedQueryResult(
            guardrail,
            content,
            BusinessAgentInvoked: false,
            BusinessFunctionInvoked: false);
    }
}
