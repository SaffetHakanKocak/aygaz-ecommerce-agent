using System.Text.Json;
using System.Text.Json.Serialization;

using Aygaz.ECommerce.Agent.Services;

namespace Aygaz.ECommerce.Agent.Models;

public sealed record OllamaChatSettings(
    IReadOnlyCollection<OllamaToolDefinition>? Tools = null,
    JsonElement? Format = null,
    double? Temperature = null,
    int? MaxOutputTokens = null,
    bool? Think = null,
    string? Model = null,
    OllamaCallType? CallType = null);

public sealed record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyCollection<OllamaChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("think")] bool Think,
    [property: JsonPropertyName("options")] OllamaRuntimeOptions Options,
    [property: JsonPropertyName("tools")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyCollection<OllamaToolDefinition>? Tools = null,
    [property: JsonPropertyName("format")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Format = null,
    [property: JsonPropertyName("keep_alive")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? KeepAlive = null);

public sealed record OllamaRuntimeOptions(
    [property: JsonPropertyName("num_ctx")] int ContextSize,
    [property: JsonPropertyName("num_predict")] int MaxOutputTokens,
    [property: JsonPropertyName("temperature")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Temperature = null);

public sealed record OllamaChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Content,
    [property: JsonPropertyName("tool_calls")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OllamaToolCall>? ToolCalls = null,
    [property: JsonPropertyName("tool_name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolName = null,
    [property: JsonPropertyName("tool_call_id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolCallId = null);

public sealed record OllamaToolCall(
    [property: JsonPropertyName("id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id = null,
    [property: JsonPropertyName("function")]
    OllamaToolCallFunction? Function = null);

public sealed record OllamaToolCallFunction(
    [property: JsonPropertyName("index")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Index = null,
    [property: JsonPropertyName("name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Name = null,
    [property: JsonPropertyName("arguments")]
    JsonElement Arguments = default);

public sealed record OllamaToolDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OllamaToolFunctionDefinition Function);

public sealed record OllamaToolFunctionDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] OllamaToolParameters Parameters);

public sealed record OllamaToolParameters(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("properties")]
    IReadOnlyDictionary<string, OllamaToolProperty> Properties,
    [property: JsonPropertyName("required")]
    IReadOnlyCollection<string> Required,
    [property: JsonPropertyName("additionalProperties")]
    bool AdditionalProperties = false);

public sealed record OllamaToolProperty(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description);

public sealed record OllamaChatResponse(
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("message")] OllamaChatMessage? Message,
    [property: JsonPropertyName("total_duration")] long? TotalDuration,
    [property: JsonPropertyName("load_duration")] long? LoadDuration,
    [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
    [property: JsonPropertyName("prompt_eval_duration")] long? PromptEvalDuration,
    [property: JsonPropertyName("eval_count")] int? EvalCount,
    [property: JsonPropertyName("eval_duration")] long? EvalDuration);

public sealed record OllamaErrorResponse(
    [property: JsonPropertyName("error")] string? Error);
