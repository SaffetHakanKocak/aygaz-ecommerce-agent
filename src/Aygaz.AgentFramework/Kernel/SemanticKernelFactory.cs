#pragma warning disable SKEXP0070

using Aygaz.AgentFramework.Configuration;
using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Kernel;

public sealed class SemanticKernelFactory : IKernelFactory
{
    private readonly SemanticKernelOptions _options;

    public SemanticKernelFactory(SemanticKernelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public Microsoft.SemanticKernel.Kernel CreateKernel()
    {
        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.Endpoint),
            Timeout = TimeSpan.FromMinutes(5)
        };

        builder.AddOllamaChatCompletion(
            modelId: _options.ModelId,
            httpClient: httpClient);

        return builder.Build();
    }
}
