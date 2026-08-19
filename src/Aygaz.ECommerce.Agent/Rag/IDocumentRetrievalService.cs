namespace Aygaz.ECommerce.Agent.Rag;

public interface IDocumentRetrievalService
{
    int DocumentCount { get; }

    int ChunkCount { get; }

    Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
