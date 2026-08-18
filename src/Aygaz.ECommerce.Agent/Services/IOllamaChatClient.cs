using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface IOllamaChatClient
{
    Task<OllamaChatMessage> ChatAsync(
        IReadOnlyCollection<OllamaChatMessage> messages,
        IReadOnlyCollection<OllamaToolDefinition>? tools = null,
        CancellationToken cancellationToken = default);
}
