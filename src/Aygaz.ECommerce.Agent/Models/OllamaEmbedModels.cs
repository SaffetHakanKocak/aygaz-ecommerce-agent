using System.Text.Json.Serialization;

namespace Aygaz.ECommerce.Agent.Models;

public sealed record OllamaEmbedRequest(
    string Model,
    string Input,
    [property: JsonPropertyName("keep_alive")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? KeepAlive = null);

public sealed record OllamaEmbedResponse(float[][] Embeddings);
