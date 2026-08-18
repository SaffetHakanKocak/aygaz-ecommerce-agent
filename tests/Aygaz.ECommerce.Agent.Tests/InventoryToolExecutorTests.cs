using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class InventoryToolExecutorTests
{
    [Fact]
    public void ToolDefinitions_ExposeExactlyTheTwoAllowedInventoryTools()
    {
        var executor = CreateExecutor(new RecordingInventoryService());

        Assert.Equal(2, executor.ToolDefinitions.Count);
        Assert.Equal(
            new[]
            {
                InventoryToolExecutor.GetProductInventoryToolName,
                InventoryToolExecutor.GetTotalProductStockToolName
            }.OrderBy(name => name),
            executor.ToolDefinitions
                .Select(definition => definition.Function.Name)
                .OrderBy(name => name));
        Assert.DoesNotContain(
            executor.ToolDefinitions,
            definition => definition.Function.Name is
                "get_all_inventory" or "get_inventory_by_location");

        AssertToolSchema(
            executor.ToolDefinitions,
            InventoryToolExecutor.GetProductInventoryToolName);
        AssertToolSchema(
            executor.ToolDefinitions,
            InventoryToolExecutor.GetTotalProductStockToolName);
    }

    [Fact]
    public async Task ExecuteAsync_ProductInventory_PassesConfiguredLimitCapsAndMinimizesLocations()
    {
        InventoryDto[] inventory = Enumerable.Range(1, 7)
            .Select(index => CreateInventory(index, index * 10))
            .ToArray();
        var service = new RecordingInventoryService
        {
            InventoryResult = inventory
        };
        var executor = CreateExecutor(service, maxLocationResults: 5);

        ToolExecutionResult result = await executor.ExecuteAsync(
            InventoryToolExecutor.GetProductInventoryToolName,
            ParseJson("""{"productId":17}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, service.InventoryCallCount);
        Assert.Equal(17, service.LastProductId);
        Assert.Equal(5, service.LastMaxResults);
        AssertNoServiceCallsExcept(service, expectedInventoryCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("found").GetBoolean());
        JsonElement[] locations = root.GetProperty("data")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(5, locations.Length);
        Assert.Equal(
            new[] { 10, 20, 30, 40, 50 },
            locations.Select(location =>
                location.GetProperty("quantityAvailable").GetInt32()));
        Assert.All(locations, AssertMinimizedInventoryShape);
    }

    [Theory]
    [InlineData(160L)]
    [InlineData(0L)]
    public async Task ExecuteAsync_TotalStock_ReturnsSingleFieldAndPreservesZero(long total)
    {
        var service = new RecordingInventoryService
        {
            TotalStockResult = total
        };
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            InventoryToolExecutor.GetTotalProductStockToolName,
            ParseJson("""{"productId":1}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, service.TotalStockCallCount);
        Assert.Equal(1, service.LastProductId);
        AssertNoServiceCallsExcept(service, expectedTotalCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement data = document.RootElement.GetProperty("data");
        JsonProperty property = Assert.Single(data.EnumerateObject());
        Assert.Equal("totalQuantityAvailable", property.Name);
        Assert.Equal(total, property.Value.GetInt64());
        Assert.False(data.TryGetProperty("productId", out _));
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public async Task ExecuteAsync_InvalidMalformedOrWrongTypeArguments_AreRejectedWithoutServiceCall(
        string toolName,
        string json)
    {
        var service = new RecordingInventoryService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson(json));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Theory]
    [InlineData(InventoryToolExecutor.GetProductInventoryToolName)]
    [InlineData(InventoryToolExecutor.GetTotalProductStockToolName)]
    public async Task ExecuteAsync_UndefinedArguments_AreRejectedWithoutServiceCall(
        string toolName)
    {
        var service = new RecordingInventoryService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(toolName, default);

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Theory]
    [InlineData("get_inventory_by_id")]
    [InlineData("get_inventory_by_location")]
    [InlineData("get_all_inventory")]
    [InlineData("create_inventory")]
    [InlineData("update_inventory")]
    [InlineData("delete_inventory")]
    [InlineData("set_stock")]
    [InlineData("adjust_stock")]
    [InlineData("execute_sql")]
    [InlineData("Get_Product_Inventory")]
    public async Task ExecuteAsync_UnknownBulkWriteOrRawTool_IsRejectedWithoutServiceCall(
        string toolName)
    {
        var service = new RecordingInventoryService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("{}"));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Fact]
    public async Task ExecuteAsync_NoLocationsOrMissingTotal_ReturnsExplicitNotFound()
    {
        var service = new RecordingInventoryService();
        var executor = CreateExecutor(service);

        ToolExecutionResult inventory = await executor.ExecuteAsync(
            InventoryToolExecutor.GetProductInventoryToolName,
            ParseJson("""{"productId":404}"""));
        ToolExecutionResult total = await executor.ExecuteAsync(
            InventoryToolExecutor.GetTotalProductStockToolName,
            ParseJson("""{"productId":404}"""));

        AssertNotFoundJson(inventory);
        AssertNotFoundJson(total);
        AssertNoServiceCallsExcept(
            service,
            expectedInventoryCalls: 1,
            expectedTotalCalls: 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Constructor_InvalidLocationLimit_Throws(int maxLocationResults)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecutor(
                new RecordingInventoryService(),
                maxLocationResults));
    }

    public static IEnumerable<object[]> InvalidArgumentCases()
    {
        string[] toolNames =
        [
            InventoryToolExecutor.GetProductInventoryToolName,
            InventoryToolExecutor.GetTotalProductStockToolName
        ];
        string[] invalidJson =
        [
            "{}",
            "null",
            "[]",
            """{"productId":null}""",
            """{"productId":"1"}""",
            """{"productId":0}""",
            """{"productId":-1}""",
            """{"productId":1.5}""",
            """{"productId":2147483648}""",
            """{"ProductId":1}""",
            """{"productId":1,"extra":true}"""
        ];

        foreach (string toolName in toolNames)
        {
            foreach (string json in invalidJson)
            {
                yield return [toolName, json];
            }
        }
    }

    private static InventoryToolExecutor CreateExecutor(
        RecordingInventoryService service,
        int maxLocationResults = 5)
    {
        return new InventoryToolExecutor(
            service,
            Options.Create(new AgentOptions
            {
                MaxInventoryLocationResults = maxLocationResults
            }),
            new RecordingToolCallLogger());
    }

    private static InventoryDto CreateInventory(int index, int quantity)
    {
        return new InventoryDto(
            index,
            17,
            $"DEMO-LOC-{index:00}",
            $"Demo Depo {index}",
            quantity,
            ReorderLevel: 5,
            UpdatedAt: new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc));
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void AssertToolSchema(
        IReadOnlyList<OllamaToolDefinition> definitions,
        string toolName)
    {
        OllamaToolDefinition definition = Assert.Single(
            definitions,
            candidate => candidate.Function.Name == toolName);
        Assert.Equal("function", definition.Type);
        Assert.False(string.IsNullOrWhiteSpace(definition.Function.Description));
        Assert.Equal("object", definition.Function.Parameters.Type);
        Assert.False(definition.Function.Parameters.AdditionalProperties);
        Assert.Equal(new[] { "productId" }, definition.Function.Parameters.Required);
        KeyValuePair<string, OllamaToolProperty> property =
            Assert.Single(definition.Function.Parameters.Properties);
        Assert.Equal("productId", property.Key);
        Assert.Equal("integer", property.Value.Type);
        Assert.False(string.IsNullOrWhiteSpace(property.Value.Description));
    }

    private static void AssertMinimizedInventoryShape(JsonElement data)
    {
        Assert.Equal(3, data.EnumerateObject().Count());
        Assert.True(data.TryGetProperty("locationCode", out _));
        Assert.True(data.TryGetProperty("locationName", out _));
        Assert.True(data.TryGetProperty("quantityAvailable", out _));
        Assert.False(data.TryGetProperty("id", out _));
        Assert.False(data.TryGetProperty("productId", out _));
        Assert.False(data.TryGetProperty("reorderLevel", out _));
        Assert.False(data.TryGetProperty("updatedAt", out _));
        Assert.False(data.TryGetProperty("product", out _));
    }

    private static void AssertRejectedJson(string content)
    {
        using JsonDocument document = JsonDocument.Parse(content);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("error").GetString()));
        Assert.False(document.RootElement.TryGetProperty("data", out _));
    }

    private static void AssertNotFoundJson(ToolExecutionResult result)
    {
        Assert.Equal(ToolExecutionStatus.NotFound, result.Status);
        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(root.GetProperty("found").GetBoolean());
        Assert.False(root.TryGetProperty("data", out _));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }

    private static void AssertNoServiceCallsExcept(
        RecordingInventoryService service,
        int expectedInventoryCalls = 0,
        int expectedTotalCalls = 0)
    {
        Assert.Equal(expectedInventoryCalls, service.InventoryCallCount);
        Assert.Equal(expectedTotalCalls, service.TotalStockCallCount);
    }

    private sealed class RecordingInventoryService : IInventoryService
    {
        public IReadOnlyList<InventoryDto> InventoryResult { get; init; } = [];

        public long? TotalStockResult { get; init; }

        public int InventoryCallCount { get; private set; }

        public int TotalStockCallCount { get; private set; }

        public int? LastProductId { get; private set; }

        public int? LastMaxResults { get; private set; }

        public Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(
            int productId,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InventoryCallCount++;
            LastProductId = productId;
            LastMaxResults = maxResults;
            return Task.FromResult(InventoryResult);
        }

        public Task<long?> GetTotalAvailableStockAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TotalStockCallCount++;
            LastProductId = productId;
            return Task.FromResult(TotalStockResult);
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
