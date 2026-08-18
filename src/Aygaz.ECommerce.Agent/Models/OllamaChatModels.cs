using System.Text.Json.Serialization;

namespace Aygaz.ECommerce.Agent.Models;

internal sealed record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyCollection<OllamaChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("think")] bool Think,
    [property: JsonPropertyName("options")] OllamaRuntimeOptions Options);

internal sealed record OllamaRuntimeOptions(
    [property: JsonPropertyName("num_gpu")] int GpuLayers,
    [property: JsonPropertyName("num_ctx")] int ContextSize,
    [property: JsonPropertyName("num_predict")] int MaxOutputTokens);

internal sealed record OllamaChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record OllamaChatResponse(
    [property: JsonPropertyName("message")] OllamaChatMessage? Message);

internal sealed record OllamaErrorResponse(
    [property: JsonPropertyName("error")] string? Error);
