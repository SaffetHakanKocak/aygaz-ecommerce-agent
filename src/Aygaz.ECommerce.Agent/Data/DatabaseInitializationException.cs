namespace Aygaz.ECommerce.Agent.Data;

public sealed class DatabaseInitializationException : Exception
{
    public DatabaseInitializationException(
        string message,
        string technicalDetails,
        Exception innerException)
        : base(message, innerException)
    {
        TechnicalDetails = technicalDetails;
    }

    public string TechnicalDetails { get; }
}
