namespace Aygaz.ECommerce.Agent.Services;

public interface ILocalLlmService
{
    Task<string> AskAsync(string userMessage, CancellationToken cancellationToken = default);
}
