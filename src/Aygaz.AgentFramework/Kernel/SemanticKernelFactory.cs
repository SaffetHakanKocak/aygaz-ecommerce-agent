#pragma warning disable SKEXP0070

using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.AgentFramework.Kernel;

public sealed class SemanticKernelFactory : IKernelFactory
{
    public const string OpenAiApiKeyVariableName = "OPENAI_API_KEY";
    public const string GroqApiKeyVariableName = "GROQ_API_KEY";
    public const string DefaultOllamaEndpoint = "http://localhost:11434";
    public const string DefaultGroqEndpoint = "https://api.groq.com/openai/v1";

    private readonly SemanticKernelOptions _options;

    public SemanticKernelFactory(SemanticKernelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public SemanticKernelProvider Provider => _options.Provider;

    public string ModelId => _options.ModelId;

    public string Endpoint => ResolveEndpoint();

    public Microsoft.SemanticKernel.Kernel CreateKernel()
    {
        if (string.IsNullOrWhiteSpace(_options.ModelId))
        {
            throw new InvalidOperationException("SemanticKernel ModelId is required.");
        }

        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();

        switch (_options.Provider)
        {
            case SemanticKernelProvider.OpenAI:
                RegisterOpenAI(builder);
                break;
            case SemanticKernelProvider.Groq:
                RegisterGroq(builder);
                break;
            case SemanticKernelProvider.Ollama:
                RegisterOllama(builder);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported SemanticKernel provider '{_options.Provider}'.");
        }

        Microsoft.SemanticKernel.Kernel kernel = builder.Build();

        if (_options.Provider == SemanticKernelProvider.Groq)
        {
            kernel = WrapWithResilientChatCompletion(kernel);
        }

        return kernel;
    }

    private static Microsoft.SemanticKernel.Kernel WrapWithResilientChatCompletion(Microsoft.SemanticKernel.Kernel kernel)
    {
        IChatCompletionService inner = kernel.GetRequiredService<IChatCompletionService>();
        if (inner is ResilientChatCompletionService)
        {
            return kernel;
        }

        var resilient = new ResilientChatCompletionService(inner, SemanticKernelProvider.Groq);
        var resilientBuilder = Microsoft.SemanticKernel.Kernel.CreateBuilder();
        resilientBuilder.Services.AddSingleton<IChatCompletionService>(resilient);
        return resilientBuilder.Build();
    }

    private void RegisterOllama(IKernelBuilder builder)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(ResolveEndpoint()),
            Timeout = TimeSpan.FromMinutes(5)
        };

        builder.AddOllamaChatCompletion(
            modelId: _options.ModelId,
            httpClient: httpClient);
    }

    private void RegisterOpenAI(IKernelBuilder builder)
    {
        string apiKey = RequireApiKey(OpenAiApiKeyVariableName, SemanticKernelProvider.OpenAI);
        builder.AddOpenAIChatCompletion(
            modelId: _options.ModelId,
            apiKey: apiKey);
    }

    private void RegisterGroq(IKernelBuilder builder)
    {
        string apiKey = RequireApiKey(GroqApiKeyVariableName, SemanticKernelProvider.Groq);
        builder.AddOpenAIChatCompletion(
            modelId: _options.ModelId,
            endpoint: new Uri(ResolveEndpoint()),
            apiKey: apiKey);
    }

    private string ResolveEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return _options.Endpoint;
        }

        return _options.Provider switch
        {
            SemanticKernelProvider.Groq => DefaultGroqEndpoint,
            _ => DefaultOllamaEndpoint
        };
    }

    private static string RequireApiKey(string variableName, SemanticKernelProvider provider)
    {
        string? apiKey = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"{variableName} environment variable is required when Provider is {provider}.");
        }

        return apiKey;
    }
}
