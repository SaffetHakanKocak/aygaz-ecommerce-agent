namespace Aygaz.ECommerce.Agent.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int GpuLayers { get; init; }

    public int ContextSize { get; init; } = 2048;

    public int MaxOutputTokens { get; init; } = 256;
}
