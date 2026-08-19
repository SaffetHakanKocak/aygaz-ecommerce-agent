namespace Aygaz.ECommerce.Agent.Services;

public sealed record OllamaCallMetrics(
    string Model,
    OllamaCallType CallType,
    long? TotalDurationNs,
    long? LoadDurationNs,
    long? PromptEvalDurationNs,
    long? EvalDurationNs,
    int? PromptEvalCount,
    int? EvalCount);
