using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Configuration;

public static class ServiceCollectionExtensions
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddLocalLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<OllamaOptions>()
            .Bind(configuration.GetRequiredSection(OllamaOptions.SectionName))
            .Validate(
                options => IsValidHttpUrl(options.BaseUrl),
                "Ollama:BaseUrl geçerli bir HTTP veya HTTPS adresi olmalıdır.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "Ollama:Model boş olamaz.")
            .Validate(
                options => options.GpuLayers >= 0,
                "Ollama:GpuLayers negatif olamaz.")
            .Validate(
                options => options.ContextSize > 0,
                "Ollama:ContextSize sıfırdan büyük olmalıdır.")
            .Validate(
                options => options.MaxOutputTokens > 0,
                "Ollama:MaxOutputTokens sıfırdan büyük olmalıdır.");

        services.AddHttpClient<IOllamaChatClient, OllamaChatClient>((serviceProvider, httpClient) =>
        {
            OllamaOptions options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;
            httpClient.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            httpClient.Timeout = RequestTimeout;
        });

        services.AddTransient<ILocalLlmService, OllamaLlmService>();

        return services;
    }

    private static bool IsValidHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
