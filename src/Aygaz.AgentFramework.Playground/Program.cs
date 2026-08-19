#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;
using IAgentFactory = Aygaz.AgentFramework.Agents.IAgentFactory;
using SemanticKernelAgentFactory = Aygaz.AgentFramework.Agents.SemanticKernelAgentFactory;

namespace Aygaz.AgentFramework.Playground;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("Aygaz Agent Framework - Semantic Kernel Playground");
        Console.WriteLine("Agent Isolation Demo");
        Console.WriteLine();

        var options = new SemanticKernelOptions
        {
            ModelId = "qwen3:1.7b",
            Endpoint = "http://localhost:11434"
        };

        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory();
        IAgentRunner runner = new SemanticKernelAgentRunner();

        // SystemAgent: ayrı Kernel, yalnızca SystemPlugin
        Microsoft.SemanticKernel.Kernel systemKernel = kernelFactory.CreateKernel();
        systemKernel.Plugins.AddFromObject(new SystemPlugin());
        systemKernel.FunctionInvocationFilters.Add(new FunctionInvocationLogger());

        var systemAgent = agentFactory.CreateAgent(new AgentDefinition
        {
            Name = "SystemAgent",
            Instructions = "You are the system info agent. Use the available functions to answer system-related questions. Do not invent function results."
        }, systemKernel);

        Console.WriteLine("Agent created: SystemAgent (Plugin: SystemPlugin)");

        // MathAgent: ayrı Kernel, yalnızca MathPlugin
        Microsoft.SemanticKernel.Kernel mathKernel = kernelFactory.CreateKernel();
        mathKernel.Plugins.AddFromObject(new MathPlugin());
        mathKernel.FunctionInvocationFilters.Add(new FunctionInvocationLogger());

        var mathAgent = agentFactory.CreateAgent(new AgentDefinition
        {
            Name = "MathAgent",
            Instructions = "You are the math agent. Use the available functions to perform calculations. Do not invent function results."
        }, mathKernel);

        Console.WriteLine("Agent created: MathAgent (Plugin: MathPlugin)");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Agent seçin:");
            Console.WriteLine("  1 - SystemAgent");
            Console.WriteLine("  2 - MathAgent");
            Console.WriteLine("  0 - Exit");
            Console.Write("> ");

            string? choice = Console.ReadLine();
            if (choice == "0" || choice == null) break;

            ChatCompletionAgent selectedAgent = choice switch
            {
                "1" => systemAgent,
                "2" => mathAgent,
                _ => systemAgent
            };

            Console.Write("Mesaj: ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            try
            {
                Console.WriteLine($"  Agent invoked: {selectedAgent.Name}");
                var response = await runner.InvokeAsync(selectedAgent, input);

                Console.WriteLine();
                Console.WriteLine(response.Content);
                Console.WriteLine($"  [{response.Duration.TotalSeconds:F1}s]");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Hata: {ex.Message}");
                Console.WriteLine();
            }
        }
    }
}
