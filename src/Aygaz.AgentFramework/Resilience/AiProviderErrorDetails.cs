using Aygaz.AgentFramework.Configuration;

namespace Aygaz.AgentFramework.Resilience;

public sealed record AiProviderErrorDetails(
    SemanticKernelProvider Provider,
    int? StatusCode,
    AiProviderErrorType ErrorType,
    string Message,
    bool IsTransient);
