using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Resilience;

namespace Aygaz.ECommerce.SemanticKernel.Web.Services;

public static class ChatErrorLogger
{
    public static void Log(
        ILogger logger,
        Exception exception,
        string sessionId,
        SemanticKernelProvider provider)
    {
        AiProviderErrorDetails details = exception switch
        {
            AiProviderTemporarilyUnavailableException unavailable => unavailable.Details,
            _ => AiProviderErrorClassifier.Classify(exception, provider)
        };

        logger.LogError(
            exception,
            "[ChatError] Provider: {Provider} Status: {Status} Type: {ErrorType} SessionId: {SessionId} Message: {Message}",
            provider,
            details.StatusCode?.ToString() ?? "n/a",
            details.ErrorType,
            sessionId,
            details.Message);
    }
}
