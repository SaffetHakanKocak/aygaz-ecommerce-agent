using System.Text.Json;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Agent.Tools;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class DocumentToolExecutorTests
{
    [Fact]
    public void ToolDefinitions_ExposeExactlyTheAllowedSearchDocumentsTool()
    {
        var executor = CreateExecutor(new RecordingDocumentRetrievalService());

        IReadOnlyList<OllamaToolDefinition> definitions = executor.ToolDefinitions;

        Assert.Single(definitions);
        Assert.Equal(
            DocumentToolExecutor.SearchDocumentsToolName,
            definitions[0].Function.Name);
        Assert.DoesNotContain(
            definitions,
            definition => definition.Function.Name == "get_all_documents");
    }

    [Fact]
    public async Task ExecuteAsync_ValidQuery_ReturnsDocumentMatches()
    {
        var retrievalService = new RecordingDocumentRetrievalService
        {
            Results =
            [
                new DocumentSearchResult(
                    "return-policy.txt",
                    "Demo iade süresi 14 gündür.",
                    0.92)
            ]
        };
        var executor = CreateExecutor(retrievalService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            DocumentToolExecutor.SearchDocumentsToolName,
            ParseJson("""{"query":"iade süresi"}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, retrievalService.CallCount);
        Assert.Equal("iade süresi", retrievalService.LastQuery);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("found").GetBoolean());
        JsonElement firstMatch = root.GetProperty("data")[0];
        Assert.Equal("return-policy.txt", firstMatch.GetProperty("documentName").GetString());
        Assert.Contains(
            "14 gün",
            firstMatch.GetProperty("text").GetString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_EmptyQuery_IsRejected(string? query)
    {
        var executor = CreateExecutor(new RecordingDocumentRetrievalService());

        ToolExecutionResult result = await executor.ExecuteAsync(
            DocumentToolExecutor.SearchDocumentsToolName,
            ParseJson($$"""{"query":"{{query}}"}"""));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatches_ReturnsDocumentsNotFound()
    {
        var retrievalService = new RecordingDocumentRetrievalService
        {
            Results = []
        };
        var executor = CreateExecutor(retrievalService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            DocumentToolExecutor.SearchDocumentsToolName,
            ParseJson("""{"query":"VIP özel iade süresi"}"""));

        Assert.Equal(ToolExecutionStatus.NotFound, result.Status);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        Assert.False(document.RootElement.GetProperty("found").GetBoolean());
        Assert.Contains(
            "demo dokümanlarda",
            document.RootElement.GetProperty("message").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("get_all_documents")]
    [InlineData("list_documents")]
    [InlineData("read_all_policies")]
    public async Task ExecuteAsync_UnknownBulkTool_IsRejected(string toolName)
    {
        var retrievalService = new RecordingDocumentRetrievalService();
        var executor = CreateExecutor(retrievalService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("""{"query":"iade"}"""));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        Assert.Equal(0, retrievalService.CallCount);
    }

    private static DocumentToolExecutor CreateExecutor(
        IDocumentRetrievalService retrievalService)
    {
        return new DocumentToolExecutor(retrievalService, new RecordingToolCallLogger());
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class RecordingDocumentRetrievalService : IDocumentRetrievalService
    {
        public int DocumentCount { get; set; } = 4;

        public int ChunkCount { get; set; } = 12;

        public IReadOnlyList<DocumentSearchResult> Results { get; set; } = [];

        public int CallCount { get; private set; }

        public string? LastQuery { get; private set; }

        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastQuery = query;
            return Task.FromResult(Results);
        }
    }

    private sealed class RecordingToolCallLogger : IToolCallLogger
    {
        public void LogToolCall(string? toolName)
        {
        }

        public void LogArguments(string argumentName, string? argumentValue)
        {
        }

        public void LogResult(ToolExecutionStatus status)
        {
        }
    }
}
