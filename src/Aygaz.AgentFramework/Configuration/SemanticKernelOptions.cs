namespace Aygaz.AgentFramework.Configuration;

public enum SemanticKernelProvider
{
    Ollama,
    OpenAI,
    Groq
}

public sealed class SemanticKernelOptions
{
    public SemanticKernelProvider Provider { get; init; } = SemanticKernelProvider.Ollama;

    public string ModelId { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public static SemanticKernelProvider ParseProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticKernelProvider.Ollama;
        }

        if (value.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticKernelProvider.OpenAI;
        }

        if (value.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            return SemanticKernelProvider.Groq;
        }

        throw new InvalidOperationException(
            $"Unknown SemanticKernel provider '{value}'. Use Ollama, OpenAI, or Groq.");
    }
}
