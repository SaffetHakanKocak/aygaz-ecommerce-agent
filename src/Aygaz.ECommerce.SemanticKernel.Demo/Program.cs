#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using System.Text;
using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Demo;

internal static class Program
{
    private static async Task Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Aygaz E-Commerce Semantic Kernel - CustomerAgent Demo");
        Console.WriteLine();

        var hostSettings = new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory
        };
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder(hostSettings);
        hostBuilder.Logging.ClearProviders();
        hostBuilder.Services.AddCustomerData(hostBuilder.Configuration);
        using IHost host = hostBuilder.Build();

        await using (AsyncServiceScope initScope = host.Services.CreateAsyncScope())
        {
            var initializer = initScope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();
        }

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
            kernelFactory,
            agentFactory,
            registry,
            router,
            [new FunctionInvocationLogger()]);
        IAgentRunner runner = new SemanticKernelAgentRunner();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        CustomerAgentRegistration.Register(registrar, customerService);
        Console.WriteLine($"Registered: {CustomerAgentRegistration.AgentName} → route: {CustomerAgentRegistration.RouteKey}");
        Console.WriteLine();

        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)
                || input.Equals("quit", StringComparison.OrdinalIgnoreCase)
                || input == "0")
                break;

            string? agentName = router.ResolveAgentName(CustomerAgentRegistration.RouteKey);
            if (agentName is null)
            {
                Console.WriteLine("  Route bulunamadı.");
                continue;
            }

            var agent = registry.GetAgent(agentName);
            Console.WriteLine($"  Route: {CustomerAgentRegistration.RouteKey} → Agent: {agent.Name}");

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

internal sealed class FunctionInvocationLogger : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        Console.WriteLine($"  KernelFunction invoked: {context.Function.Name}");
        await next(context);
    }
}
