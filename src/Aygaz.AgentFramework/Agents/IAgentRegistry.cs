using Microsoft.SemanticKernel.Agents;

namespace Aygaz.AgentFramework.Agents;

public interface IAgentRegistry
{
    void Register(ChatCompletionAgent agent);

    bool TryUnregister(string name);

    ChatCompletionAgent GetAgent(string name);

    bool TryGetAgent(string name, out ChatCompletionAgent? agent);

    IReadOnlyList<string> GetAgentNames();
}
