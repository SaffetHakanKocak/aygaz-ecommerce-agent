#pragma warning disable SKEXP0110

using Microsoft.SemanticKernel.Agents;

namespace Aygaz.AgentFramework.Agents;

public interface IAgentRegistrar
{
    ChatCompletionAgent Register(AgentRegistration registration);
}
