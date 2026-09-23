namespace Aygaz.ECommerce.Agent.Rag;

public interface IDocumentIngestionService
{
    Task<DocumentIngestionResult> IngestAsync(CancellationToken cancellationToken = default);
}
