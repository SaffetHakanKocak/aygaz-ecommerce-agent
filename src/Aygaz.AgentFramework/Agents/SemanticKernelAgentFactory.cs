#pragma warning disable SKEXP0110

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.Ollama;
using SK = Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Agents;

public sealed class SemanticKernelAgentFactory : IAgentFactory
{
    public ChatCompletionAgent CreateAgent(AgentDefinition definition, SK.Kernel kernel)
    {
        return new ChatCompletionAgent
        {
            Name = definition.Name,
            Instructions = definition.Instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(new OllamaPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };
    }
}
