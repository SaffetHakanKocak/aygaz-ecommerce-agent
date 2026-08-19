using System.Text.Json.Serialization;

namespace Aygaz.ECommerce.Agent.Models;

public sealed record OllamaEmbedRequest(
    string Model,
    string Input,
    [property: JsonPropertyName("keep_alive")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? KeepAlive = null);

public sealed record OllamaEmbedResponse(
    [property: JsonPropertyName("model")] string? Model,
    float[][] Embeddings,
    [property: JsonPropertyName("total_duration")] long? TotalDuration,
    [property: JsonPropertyName("load_duration")] long? LoadDuration,
    [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
    [property: JsonPropertyName("prompt_eval_duration")] long? PromptEvalDuration);
