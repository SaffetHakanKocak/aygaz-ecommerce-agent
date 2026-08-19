namespace Aygaz.ECommerce.Agent.Services;

public interface IOllamaEmbeddingClient
{
    Task<IReadOnlyList<float>> EmbedAsync(
        string input,
        CancellationToken cancellationToken = default);
}
