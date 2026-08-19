#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Playground;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("Aygaz Agent Framework - Semantic Kernel Playground");
        Console.WriteLine();

        // 1. Options
        var options = new SemanticKernelOptions
        {
            ModelId = "qwen3:1.7b",
            Endpoint = "http://localhost:11434"
        };

        // 2. Kernel
        var kernelFactory = new SemanticKernelFactory(options);
        Microsoft.SemanticKernel.Kernel kernel = kernelFactory.CreateKernel();

        // 3. Plugin registration (yalnızca DemoAgent'a ait plugin)
        kernel.Plugins.AddFromObject(new DemoPlugin());
        Console.WriteLine("Plugin registered: DemoPlugin");

        // 4. Function invocation filter
        kernel.FunctionInvocationFilters.Add(new FunctionInvocationLogger());

        // 5. Agent definition
        var definition = new AgentDefinition
        {
            Name = "DemoAgent",
            Instructions = "You are the demo agent for Aygaz Agent Framework. Use the available functions when they are needed. Do not invent function results."
        };

        // 6. Agent factory
        var agentFactory = new SemanticKernelAgentFactory();
        var agent = agentFactory.CreateAgent(definition, kernel);
        Console.WriteLine($"Agent created: {agent.Name}");
        Console.WriteLine();

        // 7. Agent runner
        IAgentRunner runner = new SemanticKernelAgentRunner();

        // 8. Console chat
        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)
                || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            try
            {
                var response = await runner.InvokeAsync(agent, input);

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
