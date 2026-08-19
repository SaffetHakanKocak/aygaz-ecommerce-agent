namespace Aygaz.ECommerce.Agent.Services;

public sealed class OllamaEmbeddingService(IOllamaEmbeddingClient embeddingClient) : IEmbeddingService
{
    private const string DocumentPrefix = "search_document: ";
    private const string QueryPrefix = "search_query: ";

    public Task<IReadOnlyList<float>> EmbedDocumentAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return embeddingClient.EmbedAsync(DocumentPrefix + input, cancellationToken);
    }

    public Task<IReadOnlyList<float>> EmbedQueryAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return embeddingClient.EmbedAsync(QueryPrefix + input, cancellationToken);
    }
}
