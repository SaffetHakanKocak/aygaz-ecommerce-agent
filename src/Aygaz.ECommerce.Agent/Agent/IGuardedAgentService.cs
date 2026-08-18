namespace Aygaz.ECommerce.Agent.Agent;

public interface IGuardedAgentService
{
    Task<string> AskAsync(
        string userMessage,
        CancellationToken cancellationToken = default);
}
