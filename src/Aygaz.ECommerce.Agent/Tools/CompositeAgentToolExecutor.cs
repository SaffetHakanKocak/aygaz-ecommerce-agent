using System.Collections.ObjectModel;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class CompositeAgentToolExecutor : IAgentToolExecutor
{
    private readonly IReadOnlyDictionary<string, IAgentToolModule> _modulesByToolName;
    private readonly IReadOnlyList<OllamaToolDefinition> _toolDefinitions;
    private readonly IToolCallLogger _logger;

    public CompositeAgentToolExecutor(
        IEnumerable<IAgentToolModule> toolModules,
        IToolCallLogger logger)
    {
        ArgumentNullException.ThrowIfNull(toolModules);
        ArgumentNullException.ThrowIfNull(logger);

        var modulesByToolName =
            new Dictionary<string, IAgentToolModule>(StringComparer.Ordinal);
        var toolDefinitions = new List<OllamaToolDefinition>();

        foreach (IAgentToolModule module in toolModules)
        {
            ArgumentNullException.ThrowIfNull(module);

            foreach (OllamaToolDefinition definition in module.ToolDefinitions)
            {
                ArgumentNullException.ThrowIfNull(definition);

                string toolName = definition.Function.Name;
                if (string.IsNullOrWhiteSpace(toolName))
                {
                    throw new InvalidOperationException(
                        "Tool definition name cannot be empty.");
                }

                if (!modulesByToolName.TryAdd(toolName, module))
                {
                    throw new InvalidOperationException(
                        $"Duplicate tool definition: {toolName}");
                }

                toolDefinitions.Add(definition);
            }
        }

        _modulesByToolName =
            new ReadOnlyDictionary<string, IAgentToolModule>(modulesByToolName);
        _toolDefinitions = Array.AsReadOnly(toolDefinitions.ToArray());
        _logger = logger;
    }

    public IReadOnlyList<OllamaToolDefinition> ToolDefinitions => _toolDefinitions;

    public Task<ToolExecutionResult> ExecuteAsync(
        string? toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(toolName)
            || !_modulesByToolName.TryGetValue(toolName, out IAgentToolModule? module))
        {
            _logger.LogToolCall(toolName);

            ToolExecutionResult rejected =
                ToolExecutionResult.Rejected("İstenen tool kullanılamıyor.");

            _logger.LogResult(rejected.Status);
            return Task.FromResult(rejected);
        }

        return module.ExecuteAsync(toolName, arguments, cancellationToken);
    }
}
