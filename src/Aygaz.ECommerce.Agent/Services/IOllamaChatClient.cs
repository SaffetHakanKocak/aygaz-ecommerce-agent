using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface IOllamaChatClient
{
    Task<OllamaChatMessage> ChatAsync(
        IReadOnlyCollection<OllamaChatMessage> messages,
        OllamaChatSettings? settings = null,
        CancellationToken cancellationToken = default);
}
