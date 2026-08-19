using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class OllamaEmbeddingClient : IOllamaEmbeddingClient
{
    private const string EmbedEndpoint = "api/embed";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<float>> EmbedAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var requestBody = new OllamaEmbedRequest(
            _options.EmbeddingModel,
            input,
            new OllamaEmbedOptions(_options.GpuLayers));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, EmbedEndpoint)
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

            OllamaEmbedResponse? embedResponse = JsonSerializer.Deserialize<OllamaEmbedResponse>(
                responseBody,
                JsonOptions);

            if (embedResponse?.Embeddings is not { Length: > 0 } embeddings
                || embeddings[0] is not { Length: > 0 } vector)
            {
                throw new LocalLlmException(
                    "Local LLM geçerli bir embedding döndürmedi.",
                    "Ollama yanıtında embeddings alanı boş veya eksik.");
            }

            return Array.AsReadOnly(embeddings[0]);
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
                "Local LLM embedding isteği zaman aşımına uğradı.",
                $"İstek zaman aşımı: {_httpClient.Timeout.TotalSeconds:0} saniye.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new LocalLlmException(
                "Local LLM embedding servisine bağlanılamadı.",
                $"Endpoint: {_httpClient.BaseAddress}; {exception.Message}",
                exception);
        }
        catch (JsonException exception)
        {
            throw new LocalLlmException(
                "Local LLM geçerli bir embedding yanıtı döndürmedi.",
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
                "Yapılandırılmış Ollama embedding modeli bulunamadı.",
                technicalDetails);
        }

        return new LocalLlmException(
            "Local LLM embedding isteği başarısız oldu.",
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
                return error.Trim();
            }
        }
        catch (JsonException)
        {
        }

        return responseBody.Trim();
    }
}
