namespace Aygaz.ECommerce.Agent.Rag;

public static class DocumentChunker
{
    public static IReadOnlyList<DocumentChunk> CreateChunks(
        string documentName,
        string documentText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentNullException.ThrowIfNull(documentText);

        string[] paragraphs = documentText
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (paragraphs.Length == 0)
        {
            string trimmed = documentText.Trim();
            return trimmed.Length == 0
                ? []
                : [new DocumentChunk(documentName, trimmed)];
        }

        var chunks = new List<DocumentChunk>(paragraphs.Length);

        foreach (string paragraph in paragraphs)
        {
            string normalized = paragraph.Trim();
            if (normalized.Length > 0)
            {
                chunks.Add(new DocumentChunk(documentName, normalized));
            }
        }

        return chunks;
    }

    public static IReadOnlyList<DocumentChunk> LoadDocumentsFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException(
                $"Demo doküman dizini bulunamadı: {directoryPath}");
        }

        string[] files = Directory.GetFiles(directoryPath, "*.txt", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"Demo doküman dizininde .txt dosyası bulunamadı: {directoryPath}");
        }

        var chunks = new List<DocumentChunk>();

        foreach (string filePath in files)
        {
            string documentName = Path.GetFileName(filePath);
            string content = File.ReadAllText(filePath);
            chunks.AddRange(CreateChunks(documentName, content));
        }

        return chunks;
    }
}
