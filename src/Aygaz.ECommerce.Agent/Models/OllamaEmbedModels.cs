namespace Aygaz.ECommerce.Agent.Models;

public sealed record OllamaEmbedRequest(
    string Model,
    string Input,
    OllamaEmbedOptions? Options = null);

public sealed record OllamaEmbedOptions(
    [property: System.Text.Json.Serialization.JsonPropertyName("num_gpu")]
    int GpuLayers);

public sealed record OllamaEmbedResponse(float[][] Embeddings);
