using Aygaz.ECommerce.Agent.Rag;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class DocumentChunkerTests
{
    [Fact]
    public void LoadDocumentsFromDirectory_LoadsFourDemoDocuments()
    {
        string documentsPath = GetDemoDocumentsPath();

        IReadOnlyList<DocumentChunk> chunks =
            DocumentChunker.LoadDocumentsFromDirectory(documentsPath);

        Assert.Equal(4, Directory.GetFiles(documentsPath, "*.txt").Length);
        Assert.True(chunks.Count >= 4);
        Assert.Contains(
            chunks,
            chunk => chunk.DocumentName.Equals(
                "return-policy.txt",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            chunks,
            chunk => chunk.Text.Contains("14 gün", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateChunks_SplitsParagraphsIntoDistinctChunks()
    {
        const string documentName = "demo.txt";
        const string documentText =
            "Birinci paragraf.\n\nİkinci paragraf.\n\n";

        IReadOnlyList<DocumentChunk> chunks =
            DocumentChunker.CreateChunks(documentName, documentText);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal(documentName, chunk.DocumentName));
        Assert.Equal("Birinci paragraf.", chunks[0].Text);
        Assert.Equal("İkinci paragraf.", chunks[1].Text);
    }

    private static string GetDemoDocumentsPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "data",
            "demo-documents"));
    }
}
