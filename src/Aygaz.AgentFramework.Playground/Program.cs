#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using Microsoft.SemanticKernel;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.AgentFramework.Playground;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("Aygaz Agent Framework - Semantic Kernel Playground");
        Console.WriteLine("Agent Registry + Routing Demo");
        Console.WriteLine();

        var options = new SemanticKernelOptions
        {
            ModelId = "qwen3:1.7b",
            Endpoint = "http://localhost:11434"
        };

        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory();

        // SystemAgent: ayrı Kernel, yalnızca SystemPlugin
        Microsoft.SemanticKernel.Kernel systemKernel = kernelFactory.CreateKernel();
        systemKernel.Plugins.AddFromObject(new SystemPlugin());
        systemKernel.FunctionInvocationFilters.Add(new FunctionInvocationLogger());

        var systemAgent = agentFactory.CreateAgent(new AgentDefinition
        {
            Name = "SystemAgent",
            Instructions = "You are the system info agent. Use the available functions to answer system-related questions. Do not invent function results."
        }, systemKernel);

        // MathAgent: ayrı Kernel, yalnızca MathPlugin
        Microsoft.SemanticKernel.Kernel mathKernel = kernelFactory.CreateKernel();
        mathKernel.Plugins.AddFromObject(new MathPlugin());
        mathKernel.FunctionInvocationFilters.Add(new FunctionInvocationLogger());

        var mathAgent = agentFactory.CreateAgent(new AgentDefinition
        {
            Name = "MathAgent",
            Instructions = "You are the math agent. Use the available functions to perform calculations. Do not invent function results."
        }, mathKernel);

        // Registry
        IAgentRegistry registry = new AgentRegistry();
        registry.Register(systemAgent);
        registry.Register(mathAgent);
        Console.WriteLine($"Registered agents: {string.Join(", ", registry.GetAgentNames())}");

        // Router
        IAgentRouter router = new AgentRouter();
        router.MapRoute("system", "SystemAgent");
        router.MapRoute("math", "MathAgent");
        Console.WriteLine($"Routes: {string.Join(", ", router.GetRouteKeys())}");
        Console.WriteLine();

        // Runner
        IAgentRunner runner = new SemanticKernelAgentRunner();

        while (true)
        {
            Console.WriteLine("Route seçin:");
            Console.WriteLine("  1 - system");
            Console.WriteLine("  2 - math");
            Console.WriteLine("  0 - Exit");
            Console.Write("> ");

            string? choice = Console.ReadLine();
            if (choice == "0" || choice == null) break;

            string routeKey = choice switch
            {
                "1" => "system",
                "2" => "math",
                _ => "system"
            };

            Console.Write("Mesaj: ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            string? agentName = router.ResolveAgentName(routeKey);
            if (agentName == null)
            {
                Console.WriteLine("  Route bulunamadı.");
                continue;
            }

            var agent = registry.GetAgent(agentName);

            Console.WriteLine($"  Route selected: {routeKey}");
            Console.WriteLine($"  Agent selected: {agent.Name}");

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
