#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using System.Text;
using System.Text.RegularExpressions;
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

        string providerValue = hostBuilder.Configuration["SemanticKernel:Provider"] ?? "Ollama";
        string modelId = hostBuilder.Configuration["SemanticKernel:ModelId"] ?? "qwen3:1.7b";
        string endpoint = hostBuilder.Configuration["SemanticKernel:Endpoint"] ?? "http://localhost:11434";

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            if (args[0].Equals("Ollama", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("Groq", StringComparison.OrdinalIgnoreCase))
            {
                providerValue = args[0];
                if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                {
                    modelId = args[1].Trim();
                }
            }
            else
            {
                modelId = args[0].Trim();
            }
        }

        var provider = SemanticKernelOptions.ParseProvider(providerValue);
        if (provider == SemanticKernelProvider.Groq
            && (string.IsNullOrWhiteSpace(endpoint)
                || endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                || endpoint.Contains("11434", StringComparison.OrdinalIgnoreCase)))
        {
            endpoint = SemanticKernelFactory.DefaultGroqEndpoint;
        }

        if (provider == SemanticKernelProvider.Groq
            && (string.IsNullOrWhiteSpace(modelId) || modelId.Equals("qwen3:1.7b", StringComparison.OrdinalIgnoreCase)))
        {
            modelId = "openai/gpt-oss-120b";
        }

        var options = new SemanticKernelOptions
        {
            Provider = provider,
            ModelId = modelId,
            Endpoint = endpoint
        };

        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory(options);
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
        Console.WriteLine($"Provider: {options.Provider}");
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
                if (response.InputTokens is not null || response.OutputTokens is not null)
                {
                    Console.WriteLine($"  tokens in/out: {response.InputTokens?.ToString() ?? "-"}/{response.OutputTokens?.ToString() ?? "-"}");
                }
                Console.WriteLine($"  [{response.Duration.TotalSeconds:F1}s]");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Hata: {SanitizeError(ex.Message)}");
                Console.WriteLine();
            }
        }
    }

    private static string SanitizeError(string message)
    {
        return Regex.Replace(
            message,
            @"sk-[A-Za-z0-9_\-\.*]+|gsk_[A-Za-z0-9_\-\.*]+",
            "[redacted]",
            RegexOptions.IgnoreCase);
    }
}
