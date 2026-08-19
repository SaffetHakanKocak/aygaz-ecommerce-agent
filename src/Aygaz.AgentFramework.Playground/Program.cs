#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.AgentFramework.Playground;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("Aygaz Agent Framework - Semantic Kernel Playground");
        Console.WriteLine("Agent Registration + Routing Demo");
        Console.WriteLine();

        // Framework servisleri
        var options = new SemanticKernelOptions
        {
            ModelId = "qwen3:1.7b",
            Endpoint = "http://localhost:11434"
        };

        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory();
        var registry = new AgentRegistry();
        var router = new AgentRouter();
        var registrar = new SemanticKernelAgentRegistrar(
            kernelFactory, agentFactory, registry, router,
            [new FunctionInvocationLogger()]);
        IAgentRunner runner = new SemanticKernelAgentRunner();

        // Agent registration — tek çağrı ile tüm adımlar
        registrar.Register(new AgentRegistration
        {
            RouteKey = "system",
            Definition = new AgentDefinition
            {
                Name = "SystemAgent",
                Instructions = "You are the system info agent. Use the available functions to answer system-related questions. Do not invent function results."
            },
            Plugins = [new SystemPlugin()]
        });
        Console.WriteLine("Registered: SystemAgent → route: system");

        registrar.Register(new AgentRegistration
        {
            RouteKey = "math",
            Definition = new AgentDefinition
            {
                Name = "MathAgent",
                Instructions = "You are the math agent. Use the available functions to perform calculations. Do not invent function results."
            },
            Plugins = [new MathPlugin()]
        });
        Console.WriteLine("Registered: MathAgent → route: math");
        Console.WriteLine();

        // Console
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
            Console.WriteLine($"  Route: {routeKey} → Agent: {agent.Name}");

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
