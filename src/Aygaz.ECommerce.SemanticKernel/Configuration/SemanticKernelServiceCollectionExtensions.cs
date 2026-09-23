using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.SemanticKernel.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Configuration;

public static class SemanticKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSemanticKernelCustomerStack(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CommerceOptions>()
            .Bind(configuration.GetSection(CommerceOptions.SectionName));

        services
            .AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName));

        services.AddSingleton(_ => CreateOptions(configuration));
        services.AddSingleton<SemanticKernelAgentHost>();
        services.AddSingleton<ISemanticKernelChatService, SemanticKernelChatService>();
        return services;
    }

    internal static SemanticKernelOptions CreateOptions(IConfiguration configuration)
    {
        string providerValue = configuration["SemanticKernel:Provider"] ?? "Groq";
        string modelId = configuration["SemanticKernel:ModelId"] ?? "openai/gpt-oss-120b";
        string endpoint = configuration["SemanticKernel:Endpoint"] ?? string.Empty;

        var provider = SemanticKernelOptions.ParseProvider(providerValue);
        if (provider == SemanticKernelProvider.Groq
            && (string.IsNullOrWhiteSpace(endpoint)
                || endpoint.Contains("11434", StringComparison.OrdinalIgnoreCase)))
        {
            endpoint = SemanticKernelFactory.DefaultGroqEndpoint;
        }

        if (provider == SemanticKernelProvider.Ollama
            && (string.IsNullOrWhiteSpace(modelId)
                || modelId.Equals("openai/gpt-oss-120b", StringComparison.OrdinalIgnoreCase)))
        {
            modelId = "qwen3:1.7b";
        }

        if (provider == SemanticKernelProvider.Groq
            && (string.IsNullOrWhiteSpace(modelId)
                || modelId.Equals("qwen3:1.7b", StringComparison.OrdinalIgnoreCase)))
        {
            modelId = "openai/gpt-oss-120b";
        }

        if (provider == SemanticKernelProvider.Ollama && string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = SemanticKernelFactory.DefaultOllamaEndpoint;
        }

        return new SemanticKernelOptions
        {
            Provider = provider,
            ModelId = modelId,
            Endpoint = endpoint
        };
    }
}
