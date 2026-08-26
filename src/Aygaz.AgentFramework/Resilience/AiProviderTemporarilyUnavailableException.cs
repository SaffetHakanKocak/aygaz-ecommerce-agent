namespace Aygaz.AgentFramework.Resilience;

public sealed class AiProviderTemporarilyUnavailableException : Exception
{
    public AiProviderTemporarilyUnavailableException(
        AiProviderErrorDetails details,
        Exception innerException)
        : base(details.Message, innerException)
    {
        Details = details;
    }

    public AiProviderErrorDetails Details { get; }
}
