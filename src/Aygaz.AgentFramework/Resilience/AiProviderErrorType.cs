namespace Aygaz.AgentFramework.Resilience;

public enum AiProviderErrorType
{
    RateLimit,
    Timeout,
    HttpRequest,
    ServerError,
    ProviderError,
    Authentication,
    Unexpected
}
