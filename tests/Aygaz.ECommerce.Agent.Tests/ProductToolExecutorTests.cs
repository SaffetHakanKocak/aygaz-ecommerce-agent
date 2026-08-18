using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class ProductToolExecutorTests
{
    [Fact]
    public void ToolDefinitions_ExposeExactlyTheTwoAllowedProductTools()
    {
        var executor = CreateExecutor(new RecordingProductService());

        Assert.Equal(2, executor.ToolDefinitions.Count);
        Assert.Equal(
            new[]
            {
                ProductToolExecutor.GetProductBySkuToolName,
                ProductToolExecutor.SearchProductsToolName
            }.OrderBy(name => name),
            executor.ToolDefinitions
                .Select(definition => definition.Function.Name)
                .OrderBy(name => name));
        Assert.DoesNotContain(
            executor.ToolDefinitions,
            definition => definition.Function.Name is "get_product_by_id" or "get_all_products");

        AssertToolSchema(
            executor.ToolDefinitions,
            ProductToolExecutor.GetProductBySkuToolName,
            "sku");
        AssertToolSchema(
            executor.ToolDefinitions,
            ProductToolExecutor.SearchProductsToolName,
            "query");
    }

    [Fact]
    public async Task ExecuteAsync_ValidSku_TrimsRoutesAndMinimizesInactiveProduct()
    {
        ProductDto product = CreateProduct(
            5,
            "AYG-DEMO-PRD-005",
            "Demo Product Epsilon",
            isActive: false);
        var service = new RecordingProductService
        {
            ProductBySkuResult = product
        };
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            ProductToolExecutor.GetProductBySkuToolName,
            ParseJson("""{"sku":"  AYG-DEMO-PRD-005  "}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, service.GetBySkuCallCount);
        Assert.Equal("AYG-DEMO-PRD-005", service.LastSku);
        AssertNoServiceCallsExcept(service, expectedSkuCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("found").GetBoolean());
        AssertMinimizedProduct(root.GetProperty("data"), product);
        Assert.False(root.GetProperty("data").GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task ExecuteAsync_Search_PassesConfiguredLimitCapsAndMinimizesResults()
    {
        ProductDto[] products = Enumerable.Range(1, 7)
            .Select(index => CreateProduct(
                index,
                $"AYG-DEMO-PRD-{index:000}",
                $"Demo Product {index}"))
            .ToArray();
        var service = new RecordingProductService
        {
            SearchResult = products
        };
        var executor = CreateExecutor(service, maxSearchResults: 5);

        ToolExecutionResult result = await executor.ExecuteAsync(
            ProductToolExecutor.SearchProductsToolName,
            ParseJson("""{"query":"  DemoCategoryA  "}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, service.SearchCallCount);
        Assert.Equal("DemoCategoryA", service.LastQuery);
        Assert.Equal(5, service.LastMaxResults);
        AssertNoServiceCallsExcept(service, expectedSearchCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement[] returnedProducts = document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(5, returnedProducts.Length);
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5 },
            returnedProducts.Select(item => item.GetProperty("id").GetInt32()));
        Assert.All(returnedProducts, AssertMinimizedProductShape);
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public async Task ExecuteAsync_InvalidMalformedOrWrongTypeArguments_AreRejectedWithoutServiceCall(
        string toolName,
        string json)
    {
        var service = new RecordingProductService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson(json));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Theory]
    [InlineData(ProductToolExecutor.GetProductBySkuToolName)]
    [InlineData(ProductToolExecutor.SearchProductsToolName)]
    public async Task ExecuteAsync_UndefinedArguments_AreRejectedWithoutServiceCall(
        string toolName)
    {
        var service = new RecordingProductService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(toolName, default);

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Theory]
    [InlineData("get_product_by_id")]
    [InlineData("get_all_products")]
    [InlineData("create_product")]
    [InlineData("update_product")]
    [InlineData("delete_product")]
    [InlineData("change_price")]
    [InlineData("set_stock")]
    [InlineData("execute_sql")]
    [InlineData("raw_inventory_query")]
    [InlineData("Get_Product_By_Sku")]
    public async Task ExecuteAsync_UnknownBulkWriteOrServiceOnlyTool_IsRejectedWithoutServiceCall(
        string toolName)
    {
        var service = new RecordingProductService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("{}"));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Fact]
    public async Task ExecuteAsync_MissingProductOrEmptySearch_ReturnsExplicitNotFound()
    {
        var service = new RecordingProductService();
        var executor = CreateExecutor(service);

        ToolExecutionResult bySku = await executor.ExecuteAsync(
            ProductToolExecutor.GetProductBySkuToolName,
            ParseJson("""{"sku":"AYG-DEMO-PRD-999"}"""));
        ToolExecutionResult search = await executor.ExecuteAsync(
            ProductToolExecutor.SearchProductsToolName,
            ParseJson("""{"query":"Missing Demo Product"}"""));

        AssertNotFoundJson(bySku);
        AssertNotFoundJson(search);
        AssertNoServiceCallsExcept(
            service,
            expectedSkuCalls: 1,
            expectedSearchCalls: 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Constructor_InvalidSearchLimit_Throws(int maxSearchResults)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecutor(
                new RecordingProductService(),
                maxSearchResults));
    }

    public static IEnumerable<object[]> InvalidArgumentCases()
    {
        yield return [ProductToolExecutor.GetProductBySkuToolName, "{}"];
        yield return [ProductToolExecutor.GetProductBySkuToolName, "null"];
        yield return [ProductToolExecutor.GetProductBySkuToolName, "[]"];
        yield return [ProductToolExecutor.GetProductBySkuToolName, """{"sku":null}"""];
        yield return [ProductToolExecutor.GetProductBySkuToolName, """{"sku":123}"""];
        yield return [ProductToolExecutor.GetProductBySkuToolName, """{"sku":"   "}"""];
        yield return [ProductToolExecutor.GetProductBySkuToolName, """{"Sku":"AYG-DEMO-PRD-001"}"""];
        yield return [ProductToolExecutor.GetProductBySkuToolName, """{"sku":"AYG-DEMO-PRD-001","extra":true}"""];
        yield return
        [
            ProductToolExecutor.GetProductBySkuToolName,
            JsonSerializer.Serialize(new
            {
                sku = new string('A', Product.MaximumSkuLength + 1)
            })
        ];

        yield return [ProductToolExecutor.SearchProductsToolName, "{}"];
        yield return [ProductToolExecutor.SearchProductsToolName, "null"];
        yield return [ProductToolExecutor.SearchProductsToolName, "[]"];
        yield return [ProductToolExecutor.SearchProductsToolName, """{"query":null}"""];
        yield return [ProductToolExecutor.SearchProductsToolName, """{"query":123}"""];
        yield return [ProductToolExecutor.SearchProductsToolName, """{"query":"   "}"""];
        yield return [ProductToolExecutor.SearchProductsToolName, """{"Query":"Demo Product Alpha"}"""];
        yield return [ProductToolExecutor.SearchProductsToolName, """{"query":"Demo","extra":true}"""];
        yield return
        [
            ProductToolExecutor.SearchProductsToolName,
            JsonSerializer.Serialize(new { query = new string('A', 101) })
        ];
    }

    private static ProductToolExecutor CreateExecutor(
        RecordingProductService service,
        int maxSearchResults = 5)
    {
        return new ProductToolExecutor(
            service,
            Options.Create(new AgentOptions
            {
                MaxProductSearchResults = maxSearchResults
            }),
            new RecordingToolCallLogger());
    }

    private static ProductDto CreateProduct(
        int id,
        string sku,
        string name,
        bool isActive = true)
    {
        return new ProductDto(
            id,
            sku,
            name,
            "DemoCategoryA",
            100m + id,
            isActive);
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void AssertToolSchema(
        IReadOnlyList<OllamaToolDefinition> definitions,
        string toolName,
        string argumentName)
    {
        OllamaToolDefinition definition = Assert.Single(
            definitions,
            candidate => candidate.Function.Name == toolName);
        Assert.Equal("function", definition.Type);
        Assert.False(string.IsNullOrWhiteSpace(definition.Function.Description));
        Assert.Equal("object", definition.Function.Parameters.Type);
        Assert.False(definition.Function.Parameters.AdditionalProperties);
        Assert.Equal(new[] { argumentName }, definition.Function.Parameters.Required);
        KeyValuePair<string, OllamaToolProperty> property =
            Assert.Single(definition.Function.Parameters.Properties);
        Assert.Equal(argumentName, property.Key);
        Assert.Equal("string", property.Value.Type);
        Assert.False(string.IsNullOrWhiteSpace(property.Value.Description));
    }

    private static void AssertMinimizedProduct(JsonElement data, ProductDto expected)
    {
        AssertMinimizedProductShape(data);
        Assert.Equal(expected.Id, data.GetProperty("id").GetInt32());
        Assert.Equal(expected.Sku, data.GetProperty("sku").GetString());
        Assert.Equal(expected.Name, data.GetProperty("name").GetString());
        Assert.Equal(expected.Category, data.GetProperty("category").GetString());
        Assert.Equal(expected.UnitPrice, data.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(expected.IsActive, data.GetProperty("isActive").GetBoolean());
    }

    private static void AssertMinimizedProductShape(JsonElement data)
    {
        Assert.Equal(6, data.EnumerateObject().Count());
        Assert.True(data.TryGetProperty("id", out _));
        Assert.True(data.TryGetProperty("sku", out _));
        Assert.True(data.TryGetProperty("name", out _));
        Assert.True(data.TryGetProperty("category", out _));
        Assert.True(data.TryGetProperty("unitPrice", out _));
        Assert.True(data.TryGetProperty("isActive", out _));
        Assert.False(data.TryGetProperty("createdAt", out _));
        Assert.False(data.TryGetProperty("inventoryRecords", out _));
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
        RecordingProductService service,
        int expectedIdCalls = 0,
        int expectedSkuCalls = 0,
        int expectedSearchCalls = 0)
    {
        Assert.Equal(expectedIdCalls, service.GetByIdCallCount);
        Assert.Equal(expectedSkuCalls, service.GetBySkuCallCount);
        Assert.Equal(expectedSearchCalls, service.SearchCallCount);
    }

    private sealed class RecordingProductService : IProductService
    {
        public ProductDto? ProductByIdResult { get; init; }

        public ProductDto? ProductBySkuResult { get; init; }

        public IReadOnlyList<ProductDto> SearchResult { get; init; } = [];

        public int GetByIdCallCount { get; private set; }

        public int GetBySkuCallCount { get; private set; }

        public int SearchCallCount { get; private set; }

        public int? LastId { get; private set; }

        public string? LastSku { get; private set; }

        public string? LastQuery { get; private set; }

        public int? LastMaxResults { get; private set; }

        public Task<ProductDto?> GetProductByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByIdCallCount++;
            LastId = id;
            return Task.FromResult(ProductByIdResult);
        }

        public Task<ProductDto?> GetProductBySkuAsync(
            string sku,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetBySkuCallCount++;
            LastSku = sku;
            return Task.FromResult(ProductBySkuResult);
        }

        public Task<IReadOnlyList<ProductDto>> SearchProductsAsync(
            string query,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SearchCallCount++;
            LastQuery = query;
            LastMaxResults = maxResults;
            return Task.FromResult(SearchResult);
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
