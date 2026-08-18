namespace Aygaz.ECommerce.Agent.Agent;

public interface IAgentService
{
    Task<string> AskAsync(
        string userMessage,
        CancellationToken cancellationToken = default);
}
