using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class DocumentRetrievalServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnPolicyQuery_ReturnsMostRelevantChunk()
    {
        DocumentRetrievalService service = CreateService(maxRetrievalResults: 3);

        IReadOnlyList<DocumentSearchResult> results = await service.SearchAsync("iade süresi");

        Assert.True(results.Count > 0);
        Assert.Equal("return-policy.txt", results[0].DocumentName);
        Assert.Contains("14 gün", results[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_RespectsMaximumResultLimit()
    {
        DocumentRetrievalService service = CreateService(maxRetrievalResults: 2);

        IReadOnlyList<DocumentSearchResult> results =
            await service.SearchAsync("iade teslimat kampanya destek");

        Assert.InRange(results.Count, 1, 2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_EmptyQuery_Throws(string? query)
    {
        DocumentRetrievalService service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchAsync(query!));
    }

    [Fact]
    public async Task SearchAsync_UnknownTopic_ReturnsEmptyWhenBelowSimilarityThreshold()
    {
        DocumentRetrievalService service = CreateService(minimumSimilarityScore: 0.99);

        IReadOnlyList<DocumentSearchResult> results =
            await service.SearchAsync("VIP müşterilerin özel iade süresi");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Initialize_LoadsExpectedDocumentAndChunkCounts()
    {
        DocumentRetrievalService service = CreateService();

        await service.SearchAsync("teslimat");

        Assert.Equal(4, service.DocumentCount);
        Assert.True(service.ChunkCount >= 4);
    }

    private static DocumentRetrievalService CreateService(
        int maxRetrievalResults = 3,
        double minimumSimilarityScore = 0.25)
    {
        return new DocumentRetrievalService(
            new KeywordEmbeddingService(),
            Options.Create(new RagOptions
            {
                DocumentsPath = GetRelativeDocumentsPath(),
                MaxRetrievalResults = maxRetrievalResults,
                MaxQueryLength = 500,
                MinimumSimilarityScore = minimumSimilarityScore
            }),
            new TestHostEnvironment
            {
                ContentRootPath = AppContext.BaseDirectory
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentRetrievalService>.Instance);
    }

    private static string GetRelativeDocumentsPath()
    {
        return Path.GetRelativePath(
            AppContext.BaseDirectory,
            GetDemoDocumentsPath());
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

    private sealed class KeywordEmbeddingService : IEmbeddingService
    {
        public Task<IReadOnlyList<float>> EmbedDocumentAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            return EmbedCore(input, cancellationToken);
        }

        public Task<IReadOnlyList<float>> EmbedQueryAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            return EmbedCore(input, cancellationToken);
        }

        private static Task<IReadOnlyList<float>> EmbedCore(
            string input,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            float[] vector =
            [
                ContainsAny(input, "iade") ? 1f : 0f,
                ContainsAny(input, "14 gün", "14 gun") ? 1f : 0f,
                ContainsWord(input, "süresi", "suresi") ? 1f : 0f,
                ContainsAny(input, "teslimat") ? 1f : 0f,
                ContainsAny(input, "kampanya") ? 1f : 0f,
                ContainsAny(input, "destek", "prosedür", "prosedur") ? 1f : 0f,
                ContainsAny(input, "politika", "politikasi") ? 1f : 0f,
                ContainsAny(input, "demo") ? 0.5f : 0f
            ];

            return Task.FromResult<IReadOnlyList<float>>(vector);
        }

        private static bool ContainsAny(string input, params string[] keywords)
        {
            return keywords.Any(keyword =>
                input.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsWord(string input, params string[] keywords)
        {
            return keywords.Any(keyword =>
                input.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Aygaz.ECommerce.Agent.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
