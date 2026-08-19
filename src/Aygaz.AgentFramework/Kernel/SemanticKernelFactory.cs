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

        builder.AddOllamaChatCompletion(
            modelId: _options.ModelId,
            endpoint: new Uri(_options.Endpoint));

        return builder.Build();
    }
}
