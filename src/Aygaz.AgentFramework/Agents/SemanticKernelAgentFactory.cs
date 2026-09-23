#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SK = Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Agents;

public sealed class SemanticKernelAgentFactory : IAgentFactory
{
    private readonly SemanticKernelOptions _options;

    public SemanticKernelAgentFactory(SemanticKernelOptions? options = null)
    {
        _options = options ?? new SemanticKernelOptions();
    }

    public ChatCompletionAgent CreateAgent(AgentDefinition definition, SK.Kernel kernel)
    {
        return new ChatCompletionAgent
        {
            Name = definition.Name,
            Instructions = definition.Instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(CreateExecutionSettings())
        };
    }

    private PromptExecutionSettings CreateExecutionSettings()
    {
        return _options.Provider switch
        {
            SemanticKernelProvider.OpenAI or SemanticKernelProvider.Groq => new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0
            },
            _ => new OllamaPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0
            }
        };
    }
}
