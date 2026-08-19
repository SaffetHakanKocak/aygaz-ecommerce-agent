using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Rag;

public sealed class DocumentRetrievalService : IDocumentRetrievalService
{
    private sealed record IndexedChunk(string DocumentName, string Text, IReadOnlyList<float> Embedding);

    private readonly IEmbeddingService _embeddingService;
    private readonly RagOptions _options;
    private readonly string _documentsDirectory;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private IReadOnlyList<IndexedChunk> _indexedChunks = [];
    private int _documentCount;
    private bool _isInitialized;

    public DocumentRetrievalService(
        IEmbeddingService embeddingService,
        IOptions<RagOptions> options,
        IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(embeddingService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        _embeddingService = embeddingService;
        _options = options.Value;
        _documentsDirectory = Path.GetFullPath(
            Path.Combine(hostEnvironment.ContentRootPath, _options.DocumentsPath));
    }

    public int DocumentCount => _documentCount;

    public int ChunkCount => _indexedChunks.Count;

    public async Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Geçerli bir doküman sorgusu belirtilmedi.", nameof(query));
        }

        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length > _options.MaxQueryLength)
        {
            throw new ArgumentException("Geçerli bir doküman sorgusu belirtilmedi.", nameof(query));
        }

        await EnsureInitializedAsync(cancellationToken);

        if (_indexedChunks.Count == 0)
        {
            return [];
        }

        IReadOnlyList<float> queryEmbedding = await _embeddingService.EmbedQueryAsync(
            normalizedQuery,
            cancellationToken);

        DocumentSearchResult[] rankedResults = _indexedChunks
            .Select(chunk => new DocumentSearchResult(
                chunk.DocumentName,
                chunk.Text,
                VectorMath.CosineSimilarity(queryEmbedding, chunk.Embedding)))
            .Where(result => result.Score >= _options.MinimumSimilarityScore)
            .OrderByDescending(result => result.Score)
            .Take(_options.MaxRetrievalResults)
            .ToArray();

        return rankedResults;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (_isInitialized)
            {
                return;
            }

            IReadOnlyList<DocumentChunk> chunks =
                DocumentChunker.LoadDocumentsFromDirectory(_documentsDirectory);

            _documentCount = Directory.GetFiles(
                _documentsDirectory,
                "*.txt",
                SearchOption.TopDirectoryOnly).Length;

            var indexedChunks = new List<IndexedChunk>(chunks.Count);

            foreach (DocumentChunk chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<float> embedding = await _embeddingService.EmbedDocumentAsync(
                    chunk.Text,
                    cancellationToken);

                indexedChunks.Add(new IndexedChunk(
                    chunk.DocumentName,
                    chunk.Text,
                    embedding));
            }

            _indexedChunks = indexedChunks;
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
