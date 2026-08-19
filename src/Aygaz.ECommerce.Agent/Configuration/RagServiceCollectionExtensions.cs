using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.Agent.Configuration;

public static class RagServiceCollectionExtensions
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddLocalRag(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RagOptions>()
            .Bind(configuration.GetRequiredSection(RagOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DocumentsPath),
                "Rag:DocumentsPath boş olamaz.")
            .Validate(
                options => options.MaxRetrievalResults is >= 1 and <= 10,
                "Rag:MaxRetrievalResults 1 ile 10 arasında olmalıdır.")
            .Validate(
                options => options.MaxQueryLength is >= 1 and <= 2000,
                "Rag:MaxQueryLength 1 ile 2000 arasında olmalıdır.")
            .Validate(
                options => options.MinimumSimilarityScore is >= 0d and <= 1d,
                "Rag:MinimumSimilarityScore 0 ile 1 arasında olmalıdır.");

        services.AddHttpClient<IOllamaEmbeddingClient, OllamaEmbeddingClient>(
            (serviceProvider, httpClient) =>
            {
                OllamaOptions options = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>()
                    .Value;
                httpClient.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
                httpClient.Timeout = RequestTimeout;
            });

        services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
        services.AddSingleton<IDocumentRetrievalService, DocumentRetrievalService>();

        return services;
    }
}
