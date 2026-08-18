using System.Text.Json;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Tools;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class CompositeAgentToolExecutorTests
{
    private static readonly string[] CustomerToolNames =
    [
        CustomerToolExecutor.GetCustomerByEmailToolName,
        CustomerToolExecutor.GetCustomerByIdToolName,
        CustomerToolExecutor.SearchCustomersByNameToolName
    ];

    private static readonly string[] OrderToolNames =
    [
        OrderToolExecutor.GetOrderByNumberToolName,
        OrderToolExecutor.GetCustomerOrdersToolName,
        OrderToolExecutor.GetLatestCustomerOrderToolName
    ];

    private static readonly string[] ProductToolNames =
    [
        ProductToolExecutor.GetProductBySkuToolName,
        ProductToolExecutor.SearchProductsToolName
    ];

    private static readonly string[] InventoryToolNames =
    [
        InventoryToolExecutor.GetProductInventoryToolName,
        InventoryToolExecutor.GetTotalProductStockToolName
    ];

    [Fact]
    public void ToolDefinitions_ExposeExactUnionOfTenReadOnlyTools()
    {
        var customerModule = new RecordingToolModule(CustomerToolNames);
        var orderModule = new RecordingToolModule(OrderToolNames);
        var productModule = new RecordingToolModule(ProductToolNames);
        var inventoryModule = new RecordingToolModule(InventoryToolNames);
        var executor = new CompositeAgentToolExecutor(
            [customerModule, orderModule, productModule, inventoryModule],
            new RecordingToolCallLogger());

        Assert.Equal(10, executor.ToolDefinitions.Count);
        Assert.Equal(
            CustomerToolNames
                .Concat(OrderToolNames)
                .Concat(ProductToolNames)
                .Concat(InventoryToolNames),
            executor.ToolDefinitions.Select(definition => definition.Function.Name));
        Assert.Equal(
            10,
            executor.ToolDefinitions
                .Select(definition => definition.Function.Name)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public async Task ExecuteAsync_KnownTools_DispatchOnlyToOwningModule()
    {
        ToolExecutionResult customerResult =
            ToolExecutionResult.FromSuccess(new { id = 1 });
        ToolExecutionResult orderResult =
            ToolExecutionResult.FromSuccess(new { orderNumber = "AYG-DEMO-1004" });
        ToolExecutionResult productResult =
            ToolExecutionResult.FromSuccess(new { sku = "AYG-DEMO-PRD-001" });
        ToolExecutionResult inventoryResult =
            ToolExecutionResult.FromSuccess(new { totalQuantityAvailable = 160 });
        var customerModule = new RecordingToolModule(
            CustomerToolNames,
            customerResult);
        var orderModule = new RecordingToolModule(
            OrderToolNames,
            orderResult);
        var productModule = new RecordingToolModule(
            ProductToolNames,
            productResult);
        var inventoryModule = new RecordingToolModule(
            InventoryToolNames,
            inventoryResult);
        var executor = new CompositeAgentToolExecutor(
            [customerModule, orderModule, productModule, inventoryModule],
            new RecordingToolCallLogger());
        JsonElement customerArguments = ParseJson(
            """{"email":"ahmet.yilmaz@example.com"}""");
        JsonElement orderArguments = ParseJson("""{"customerId":1}""");
        JsonElement productArguments = ParseJson(
            """{"sku":"AYG-DEMO-PRD-001"}""");
        JsonElement inventoryArguments = ParseJson("""{"productId":1}""");

        ToolExecutionResult actualCustomerResult = await executor.ExecuteAsync(
            CustomerToolExecutor.GetCustomerByEmailToolName,
            customerArguments);
        ToolExecutionResult actualOrderResult = await executor.ExecuteAsync(
            OrderToolExecutor.GetLatestCustomerOrderToolName,
            orderArguments);
        ToolExecutionResult actualProductResult = await executor.ExecuteAsync(
            ProductToolExecutor.GetProductBySkuToolName,
            productArguments);
        ToolExecutionResult actualInventoryResult = await executor.ExecuteAsync(
            InventoryToolExecutor.GetTotalProductStockToolName,
            inventoryArguments);

        Assert.Same(customerResult, actualCustomerResult);
        Assert.Same(orderResult, actualOrderResult);
        Assert.Same(productResult, actualProductResult);
        Assert.Same(inventoryResult, actualInventoryResult);
        ToolInvocation customerCall = Assert.Single(customerModule.Calls);
        Assert.Equal(CustomerToolExecutor.GetCustomerByEmailToolName, customerCall.ToolName);
        Assert.Equal(
            "ahmet.yilmaz@example.com",
            customerCall.Arguments.GetProperty("email").GetString());
        ToolInvocation orderCall = Assert.Single(orderModule.Calls);
        Assert.Equal(OrderToolExecutor.GetLatestCustomerOrderToolName, orderCall.ToolName);
        Assert.Equal(1, orderCall.Arguments.GetProperty("customerId").GetInt32());
        ToolInvocation productCall = Assert.Single(productModule.Calls);
        Assert.Equal(ProductToolExecutor.GetProductBySkuToolName, productCall.ToolName);
        Assert.Equal(
            "AYG-DEMO-PRD-001",
            productCall.Arguments.GetProperty("sku").GetString());
        ToolInvocation inventoryCall = Assert.Single(inventoryModule.Calls);
        Assert.Equal(InventoryToolExecutor.GetTotalProductStockToolName, inventoryCall.ToolName);
        Assert.Equal(1, inventoryCall.Arguments.GetProperty("productId").GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("get_all_orders")]
    [InlineData("create_order")]
    [InlineData("delete_order")]
    [InlineData("get_all_products")]
    [InlineData("get_all_inventory")]
    [InlineData("set_stock")]
    [InlineData("change_price")]
    [InlineData("delete_product")]
    [InlineData("raw_inventory_query")]
    [InlineData("execute_sql")]
    [InlineData("Get_Order_By_Number")]
    public async Task ExecuteAsync_UnknownWriteBulkOrSqlTool_IsRejectedWithoutModuleCall(
        string? toolName)
    {
        var customerModule = new RecordingToolModule(CustomerToolNames);
        var orderModule = new RecordingToolModule(OrderToolNames);
        var productModule = new RecordingToolModule(ProductToolNames);
        var inventoryModule = new RecordingToolModule(InventoryToolNames);
        var logger = new RecordingToolCallLogger();
        var executor = new CompositeAgentToolExecutor(
            [customerModule, orderModule, productModule, inventoryModule],
            logger);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("{}"));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        Assert.Empty(customerModule.Calls);
        Assert.Empty(orderModule.Calls);
        Assert.Empty(productModule.Calls);
        Assert.Empty(inventoryModule.Calls);
        Assert.Equal(toolName, Assert.Single(logger.ToolNames));
        Assert.Equal(ToolExecutionStatus.Rejected, Assert.Single(logger.Results));

        using JsonDocument document = JsonDocument.Parse(result.Content);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("error").GetString()));
    }

    [Fact]
    public void Constructor_DuplicateToolNameAcrossModules_ThrowsBeforePublishingDefinitions()
    {
        var firstModule = new RecordingToolModule(["duplicate_tool"]);
        var secondModule = new RecordingToolModule(["duplicate_tool"]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new CompositeAgentToolExecutor(
                [firstModule, secondModule],
                new RecordingToolCallLogger()));

        Assert.Contains("duplicate_tool", exception.Message);
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static OllamaToolDefinition CreateDefinition(string toolName)
    {
        return new OllamaToolDefinition(
            "function",
            new OllamaToolFunctionDefinition(
                toolName,
                "Test tool",
                new OllamaToolParameters(
                    "object",
                    new Dictionary<string, OllamaToolProperty>(),
                    [],
                    AdditionalProperties: false)));
    }

    private sealed class RecordingToolModule : IAgentToolModule
    {
        private readonly ToolExecutionResult _result;

        public RecordingToolModule(
            IEnumerable<string> toolNames,
            ToolExecutionResult? result = null)
        {
            ToolDefinitions = toolNames.Select(CreateDefinition).ToArray();
            _result = result ?? ToolExecutionResult.FromSuccess(new { ok = true });
        }

        public IReadOnlyList<OllamaToolDefinition> ToolDefinitions { get; }

        public List<ToolInvocation> Calls { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(
            string? toolName,
            JsonElement arguments,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new ToolInvocation(toolName, arguments.Clone()));
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingToolCallLogger : IToolCallLogger
    {
        public List<string?> ToolNames { get; } = [];

        public List<ToolExecutionStatus> Results { get; } = [];

        public void LogToolCall(string? toolName)
        {
            ToolNames.Add(toolName);
        }

        public void LogArguments(string argumentName, string? argumentValue)
        {
        }

        public void LogResult(ToolExecutionStatus status)
        {
            Results.Add(status);
        }
    }

    private sealed record ToolInvocation(string? ToolName, JsonElement Arguments);
}
