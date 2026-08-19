using Microsoft.SemanticKernel.Agents;
using SK = Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Agents;

public interface IAgentFactory
{
    ChatCompletionAgent CreateAgent(AgentDefinition definition, SK.Kernel kernel);
}
