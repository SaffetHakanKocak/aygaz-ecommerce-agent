namespace Aygaz.ECommerce.Agent.Rag;

public sealed record DocumentChunk(string DocumentName, string Text);

public sealed record DocumentSearchResult(string DocumentName, string Text, double Score);
