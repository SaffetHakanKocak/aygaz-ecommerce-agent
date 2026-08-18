using System.Text.Json;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Tools;

public interface IAgentToolExecutor
{
    IReadOnlyList<OllamaToolDefinition> ToolDefinitions { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        string? toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
