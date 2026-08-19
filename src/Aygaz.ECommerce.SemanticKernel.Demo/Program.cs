#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using System.Text;
using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Observability;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aygaz.ECommerce.SemanticKernel.Demo;

internal static class Program
{
    private static async Task Main(string[] args)
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

        string modelId = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0].Trim()
            : hostBuilder.Configuration["SemanticKernel:ModelId"] ?? "qwen3:1.7b";
        string endpoint = hostBuilder.Configuration["SemanticKernel:Endpoint"] ?? "http://localhost:11434";

        var options = new SemanticKernelOptions
        {
            ModelId = modelId,
            Endpoint = endpoint
        };

        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory();
        var registry = new AgentRegistry();
        var router = new AgentRouter();
        var telemetry = new KernelInvocationTelemetry();
        var telemetryFilter = new KernelInvocationTelemetryFilter(telemetry);
        var registrar = new SemanticKernelAgentRegistrar(
            kernelFactory,
            agentFactory,
            registry,
            router,
            [telemetryFilter],
            [telemetryFilter]);
        IAgentRunner runner = new SemanticKernelAgentRunner();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        CustomerAgentRegistration.Register(registrar, customerService);
        Console.WriteLine($"Model: {options.ModelId}");
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
                telemetry.Reset();
                var response = await runner.InvokeAsync(agent, input);
                Console.WriteLine();
                Console.WriteLine(response.Content);
                Console.WriteLine($"  function: {telemetry.LastFunctionName ?? "(none)"}");
                Console.WriteLine($"  function invocation count: {telemetry.FunctionInvocationCount}");
                Console.WriteLine($"  LLM inference count: {telemetry.EstimateLlmInferenceCount()}");
                Console.WriteLine($"  fast path: {(telemetry.Terminated ? "yes" : "no")}");
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
