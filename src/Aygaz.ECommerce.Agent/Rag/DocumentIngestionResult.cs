namespace Aygaz.ECommerce.Agent.Rag;

public sealed record DocumentIngestionResult(
    int DocumentCount,
    int ChunkCount,
    int UpsertedChunkCount);
