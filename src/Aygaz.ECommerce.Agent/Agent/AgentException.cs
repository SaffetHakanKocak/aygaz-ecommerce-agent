namespace Aygaz.ECommerce.Agent.Agent;

public sealed class AgentException : Exception
{
    public AgentException(
        string message,
        string technicalDetails,
        Exception? innerException = null)
        : base(message, innerException)
    {
        TechnicalDetails = technicalDetails;
    }

    public string TechnicalDetails { get; }
}
