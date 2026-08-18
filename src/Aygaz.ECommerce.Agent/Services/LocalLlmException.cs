namespace Aygaz.ECommerce.Agent.Services;

public sealed class LocalLlmException : Exception
{
    public LocalLlmException(
        string message,
        string technicalDetails,
        Exception? innerException = null)
        : base(message, innerException)
    {
        TechnicalDetails = technicalDetails;
    }

    public string TechnicalDetails { get; }
}
