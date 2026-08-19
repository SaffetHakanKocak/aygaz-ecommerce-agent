namespace Aygaz.ECommerce.Agent.Services;

public interface IEmbeddingService
{
    Task<IReadOnlyList<float>> EmbedDocumentAsync(
        string input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float>> EmbedQueryAsync(
        string input,
        CancellationToken cancellationToken = default);
}
