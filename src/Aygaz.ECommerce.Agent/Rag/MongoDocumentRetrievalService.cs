using System.Security.Cryptography;
using System.Text;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.Rag;

public sealed class MongoDocumentRetrievalService : IDocumentRetrievalService, IDocumentIngestionService
{
    private sealed record IndexedChunk(
        string DocumentName,
        int ChunkIndex,
        string Text,
        IReadOnlyList<float> Embedding);

    private readonly IMongoCollection<BsonDocument> _documentChunks;
    private readonly IEmbeddingService _embeddingService;
    private readonly RagOptions _options;
    private readonly string _documentsDirectory;
    private readonly ILogger<MongoDocumentRetrievalService> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private int _documentCount;
    private int _chunkCount;
    private bool _isInitialized;

    public MongoDocumentRetrievalService(
        IMongoDatabase database,
        IEmbeddingService embeddingService,
        IOptions<RagOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<MongoDocumentRetrievalService> logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(embeddingService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(logger);

        _documentChunks = database.GetCollection<BsonDocument>(MongoCollectionSetup.DocumentChunks);
        _embeddingService = embeddingService;
        _options = options.Value;
        _logger = logger;
        _documentsDirectory = ResolveDocumentsDirectory(
            hostEnvironment.ContentRootPath,
            _options.DocumentsPath);
    }

    public int DocumentCount => _documentCount;

    public int ChunkCount => _chunkCount;

    public async Task<DocumentIngestionResult> IngestAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentChunk> chunks = DocumentChunker.LoadDocumentsFromDirectory(_documentsDirectory);
        int documentCount = Directory.GetFiles(
            _documentsDirectory,
            "*.txt",
            SearchOption.TopDirectoryOnly).Length;

        var upserts = new List<WriteModel<BsonDocument>>(chunks.Count);
        DateTime ingestedAt = DateTime.UtcNow;

        for (int i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentChunk chunk = chunks[i];
            IReadOnlyList<float> embedding = await _embeddingService.EmbedDocumentAsync(
                chunk.Text,
                cancellationToken);
            string contentHash = CreateContentHash(chunk.DocumentName, chunk.Text);

            var replacement = new BsonDocument
            {
                ["contentHash"] = contentHash,
                ["documentName"] = chunk.DocumentName,
                ["chunkIndex"] = i,
                ["text"] = chunk.Text,
                ["embedding"] = new BsonArray(embedding.Select(value => new BsonDouble(value))),
                ["ingestedAt"] = ingestedAt
            };

            upserts.Add(new ReplaceOneModel<BsonDocument>(
                Builders<BsonDocument>.Filter.Eq("contentHash", contentHash),
                replacement)
            {
                IsUpsert = true
            });
        }

        if (upserts.Count > 0)
        {
            await _documentChunks.BulkWriteAsync(
                upserts,
                new BulkWriteOptions { IsOrdered = true },
                cancellationToken);
        }

        _documentCount = documentCount;
        _chunkCount = chunks.Count;
        _isInitialized = true;

        _logger.LogInformation(
            "RAG Mongo ingest hazir: {DocumentCount} dokuman, {ChunkCount} chunk.",
            _documentCount,
            _chunkCount);

        return new DocumentIngestionResult(documentCount, chunks.Count, upserts.Count);
    }

    public async Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Gecerli bir dokuman sorgusu belirtilmedi.", nameof(query));
        }

        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length > _options.MaxQueryLength)
        {
            throw new ArgumentException("Gecerli bir dokuman sorgusu belirtilmedi.", nameof(query));
        }

        await EnsureInitializedAsync(cancellationToken);

        if (_chunkCount == 0)
        {
            return [];
        }

        IReadOnlyList<float> queryEmbedding = await _embeddingService.EmbedQueryAsync(
            normalizedQuery,
            cancellationToken);
        IReadOnlyList<IndexedChunk> chunks = await LoadIndexedChunksAsync(cancellationToken);

        return chunks
            .Select(chunk => new DocumentSearchResult(
                chunk.DocumentName,
                chunk.Text,
                VectorMath.CosineSimilarity(queryEmbedding, chunk.Embedding)))
            .Where(result => result.Score >= _options.MinimumSimilarityScore)
            .OrderByDescending(result => result.Score)
            .Take(_options.MaxRetrievalResults)
            .ToArray();
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

            long existingChunks = await _documentChunks.CountDocumentsAsync(
                FilterDefinition<BsonDocument>.Empty,
                cancellationToken: cancellationToken);
            if (existingChunks == 0 && _options.AutoIngestOnStartup)
            {
                await IngestAsync(cancellationToken);
                return;
            }

            _chunkCount = checked((int)existingChunks);
            using IAsyncCursor<string> documents = await _documentChunks.DistinctAsync<string>(
                "documentName",
                FilterDefinition<BsonDocument>.Empty,
                cancellationToken: cancellationToken);
            _documentCount = (await documents.ToListAsync(cancellationToken)).Count;
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<IReadOnlyList<IndexedChunk>> LoadIndexedChunksAsync(CancellationToken cancellationToken)
    {
        List<BsonDocument> documents = await _documentChunks
            .Find(FilterDefinition<BsonDocument>.Empty)
            .SortBy(document => document["documentName"])
            .ThenBy(document => document["chunkIndex"])
            .ToListAsync(cancellationToken);

        return documents
            .Select(document => new IndexedChunk(
                document["documentName"].AsString,
                document["chunkIndex"].AsInt32,
                document["text"].AsString,
                document["embedding"].AsBsonArray.Select(value => (float)value.ToDouble()).ToArray()))
            .ToArray();
    }

    private static string CreateContentHash(string documentName, string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(documentName + "\n" + text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveDocumentsDirectory(string contentRootPath, string documentsPath)
    {
        string contentRootCandidate = Path.GetFullPath(
            Path.Combine(contentRootPath, documentsPath));
        if (Directory.Exists(contentRootCandidate))
        {
            return contentRootCandidate;
        }

        string outputCandidate = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, documentsPath));
        return Directory.Exists(outputCandidate)
            ? outputCandidate
            : contentRootCandidate;
    }
}
