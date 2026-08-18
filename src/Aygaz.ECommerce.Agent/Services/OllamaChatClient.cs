using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class OllamaChatClient : IOllamaChatClient
{
    private const string ChatEndpoint = "api/chat";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaChatClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<OllamaChatMessage> ChatAsync(
        IReadOnlyCollection<OllamaChatMessage> messages,
        IReadOnlyCollection<OllamaToolDefinition>? tools = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            throw new ArgumentException("En az bir sohbet mesajı gereklidir.", nameof(messages));
        }

        var requestBody = new OllamaChatRequest(
            _options.Model,
            messages,
            Stream: false,
            Think: false,
            Options: new OllamaRuntimeOptions(
                _options.GpuLayers,
                _options.ContextSize,
                _options.MaxOutputTokens),
            Tools: tools);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint)
            {
                Content = JsonContent.Create(requestBody, options: JsonOptions)
            };

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateRequestException(response.StatusCode, responseBody);
            }

            OllamaChatResponse? chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(
                responseBody,
                JsonOptions);

            if (chatResponse?.Message is null)
            {
                throw new LocalLlmException(
                    "Local LLM geçerli bir yanıt döndürmedi.",
                    "Ollama yanıtında message alanı boş veya eksik.");
            }

            return chatResponse.Message;
        }
        catch (LocalLlmException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException exception)
        {
            throw new LocalLlmException(
                "Local LLM yanıtı zaman aşımına uğradı. Ollama servisinin çalıştığını kontrol edin.",
                $"İstek zaman aşımı: {_httpClient.Timeout.TotalSeconds:0} saniye.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new LocalLlmException(
                "Local LLM'e bağlanılamadı. Ollama servisinin çalıştığını kontrol edin.",
                $"Endpoint: {_httpClient.BaseAddress}; {exception.Message}",
                exception);
        }
        catch (JsonException exception)
        {
            throw new LocalLlmException(
                "Local LLM geçerli bir yanıt döndürmedi.",
                $"Ollama yanıtı JSON olarak okunamadı: {exception.Message}",
                exception);
        }
    }

    private static LocalLlmException CreateRequestException(
        HttpStatusCode statusCode,
        string responseBody)
    {
        string ollamaError = GetOllamaError(responseBody);
        string technicalDetails =
            $"HTTP {(int)statusCode} ({statusCode}); Ollama: {ollamaError}";

        if (statusCode == HttpStatusCode.NotFound
            && ollamaError.Contains("model", StringComparison.OrdinalIgnoreCase))
        {
            return new LocalLlmException(
                "Yapılandırılmış Ollama modeli bulunamadı.",
                technicalDetails);
        }

        return new LocalLlmException(
            "Local LLM isteği başarısız oldu.",
            technicalDetails);
    }

    private static string GetOllamaError(string responseBody)
    {
        try
        {
            string? error = JsonSerializer.Deserialize<OllamaErrorResponse>(
                responseBody,
                JsonOptions)?.Error;

            if (!string.IsNullOrWhiteSpace(error))
            {
                return NormalizeTechnicalDetail(error);
            }
        }
        catch (JsonException)
        {
            // Ham yanıt aşağıda kısa ve tek satırlı bir teknik detay olarak korunur.
        }

        return NormalizeTechnicalDetail(responseBody);
    }

    private static string NormalizeTechnicalDetail(string value)
    {
        const int maximumLength = 500;
        string normalized = value.ReplaceLineEndings(" ").Trim();

        if (normalized.Length == 0)
        {
            return "Yanıt gövdesi boş.";
        }

        return normalized.Length <= maximumLength
            ? normalized
            : $"{normalized[..maximumLength]}...";
    }
}
