namespace Aygaz.ECommerce.Agent.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string EmbeddingModel { get; init; } = "nomic-embed-text";

    public string KeepAlive { get; init; } = "5m";

    public int ContextSize { get; init; } = 2048;

    public int MaxOutputTokens { get; init; } = 256;
}
