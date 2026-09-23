namespace Aygaz.ECommerce.Agent.Configuration;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public string StoreProvider { get; init; } = "MongoDb";

    public string EmbeddingProvider { get; init; } = "Lexical";

    public string DocumentsPath { get; init; } = "data/demo-documents";

    public int MaxRetrievalResults { get; init; } = 3;

    public int MaxQueryLength { get; init; } = 500;

    public double MinimumSimilarityScore { get; init; } = 0.25;

    public bool AutoIngestOnStartup { get; init; } = true;
}
