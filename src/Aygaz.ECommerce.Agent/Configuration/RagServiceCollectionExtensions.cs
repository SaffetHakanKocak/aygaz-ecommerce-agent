using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Configuration;

public static class RagServiceCollectionExtensions
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddLocalRag(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        RagOptions ragOptions = configuration
            .GetSection(RagOptions.SectionName)
            .Get<RagOptions>() ?? new RagOptions();

        services
            .AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DocumentsPath),
                "Rag:DocumentsPath bos olamaz.")
            .Validate(
                options => IsKnownStoreProvider(options.StoreProvider),
                "Rag:StoreProvider yalnizca MongoDb veya Local olabilir.")
            .Validate(
                options => IsKnownEmbeddingProvider(options.EmbeddingProvider),
                "Rag:EmbeddingProvider yalnizca Lexical veya Ollama olabilir.")
            .Validate(
                options => options.MaxRetrievalResults is >= 1 and <= 10,
                "Rag:MaxRetrievalResults 1 ile 10 arasinda olmalidir.")
            .Validate(
                options => options.MaxQueryLength is >= 1 and <= 2000,
                "Rag:MaxQueryLength 1 ile 2000 arasinda olmalidir.")
            .Validate(
                options => options.MinimumSimilarityScore is >= 0d and <= 1d,
                "Rag:MinimumSimilarityScore 0 ile 1 arasinda olmalidir.");

        if (ragOptions.EmbeddingProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IOllamaCallTracker, OllamaCallTracker>();
            services.TryAddSingleton<IOllamaPerformanceLogger, OllamaPerformanceLogger>();
            services
                .AddOptions<OllamaOptions>()
                .Bind(configuration.GetSection(OllamaOptions.SectionName));

            services.AddHttpClient<IOllamaEmbeddingClient, OllamaEmbeddingClient>(
                (serviceProvider, httpClient) =>
                {
                    OllamaOptions options = serviceProvider
                        .GetRequiredService<IOptions<OllamaOptions>>()
                        .Value;
                    httpClient.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
                    httpClient.Timeout = RequestTimeout;
                });

            services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
        }
        else
        {
            services.AddSingleton<IEmbeddingService, LexicalEmbeddingService>();
        }

        if (ragOptions.StoreProvider.Equals("MongoDb", StringComparison.OrdinalIgnoreCase)
            || ragOptions.StoreProvider.Equals("Mongo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<MongoDocumentRetrievalService>();
            services.AddSingleton<IDocumentRetrievalService>(serviceProvider =>
                serviceProvider.GetRequiredService<MongoDocumentRetrievalService>());
            services.AddSingleton<IDocumentIngestionService>(serviceProvider =>
                serviceProvider.GetRequiredService<MongoDocumentRetrievalService>());
        }
        else
        {
            services.AddSingleton<IDocumentRetrievalService, DocumentRetrievalService>();
        }

        return services;
    }

    private static bool IsKnownStoreProvider(string? value)
    {
        return value?.Equals("MongoDb", StringComparison.OrdinalIgnoreCase) == true
            || value?.Equals("Mongo", StringComparison.OrdinalIgnoreCase) == true
            || value?.Equals("Local", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsKnownEmbeddingProvider(string? value)
    {
        return value?.Equals("Lexical", StringComparison.OrdinalIgnoreCase) == true
            || value?.Equals("Ollama", StringComparison.OrdinalIgnoreCase) == true;
    }
}
