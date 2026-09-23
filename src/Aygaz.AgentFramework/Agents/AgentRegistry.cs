#pragma warning disable SKEXP0110

using System.Collections.Concurrent;
using Microsoft.SemanticKernel.Agents;

namespace Aygaz.AgentFramework.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, ChatCompletionAgent> _agents = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ChatCompletionAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        string name = agent.Name ?? throw new ArgumentException("Agent must have a name.");

        if (!_agents.TryAdd(name, agent))
        {
            throw new InvalidOperationException($"Agent '{name}' is already registered.");
        }
    }

    public bool TryUnregister(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _agents.TryRemove(name, out _);
    }

    public ChatCompletionAgent GetAgent(string name)
    {
        if (_agents.TryGetValue(name, out var agent))
            return agent;

        throw new KeyNotFoundException($"Agent '{name}' not found in registry.");
    }

    public bool TryGetAgent(string name, out ChatCompletionAgent? agent)
    {
        return _agents.TryGetValue(name, out agent);
    }

    public IReadOnlyList<string> GetAgentNames()
    {
        return _agents.Keys.ToList().AsReadOnly();
    }
}
