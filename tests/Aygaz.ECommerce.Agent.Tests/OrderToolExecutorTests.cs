using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class OrderToolExecutorTests
{
    [Fact]
    public void ToolDefinitions_ExposeExactlyTheThreeAllowedOrderTools()
    {
        var executor = CreateExecutor(new RecordingOrderService());

        IReadOnlyList<OllamaToolDefinition> definitions = executor.ToolDefinitions;

        Assert.Equal(3, definitions.Count);
        Assert.Equal(
            new[]
            {
                OrderToolExecutor.GetOrderByNumberToolName,
                OrderToolExecutor.GetCustomerOrdersToolName,
                OrderToolExecutor.GetLatestCustomerOrderToolName
            }.OrderBy(name => name),
            definitions.Select(definition => definition.Function.Name).OrderBy(name => name));
        Assert.DoesNotContain(
            definitions,
            definition => definition.Function.Name is "get_order_by_id" or "get_all_orders");

        AssertToolSchema(
            definitions,
            OrderToolExecutor.GetOrderByNumberToolName,
            "orderNumber",
            "string");
        AssertToolSchema(
            definitions,
            OrderToolExecutor.GetCustomerOrdersToolName,
            "customerId",
            "integer");
        AssertToolSchema(
            definitions,
            OrderToolExecutor.GetLatestCustomerOrderToolName,
            "customerId",
            "integer");
    }

    [Fact]
    public async Task ExecuteAsync_ValidOrderNumber_TrimsRoutesAndMinimizesResult()
    {
        OrderDto order = CreateOrder(4, "AYG-DEMO-1004", OrderStatus.Preparing);
        var orderService = new RecordingOrderService
        {
            OrderByNumberResult = order
        };
        var executor = CreateExecutor(orderService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            OrderToolExecutor.GetOrderByNumberToolName,
            ParseJson("""{"orderNumber":"  AYG-DEMO-1004  "}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, orderService.GetByNumberCallCount);
        Assert.Equal("AYG-DEMO-1004", orderService.LastOrderNumber);
        AssertNoOtherServiceCalls(orderService, expectedNumberCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("found").GetBoolean());
        AssertMinimizedOrder(root.GetProperty("data"), order, "Haz\u0131rlan\u0131yor");
    }

    [Fact]
    public async Task ExecuteAsync_CustomerOrders_PassesConfiguredLimitAndCapsReturnedData()
    {
        OrderDto[] orders = Enumerable.Range(1, 7)
            .Select(index => CreateOrder(
                index,
                $"AYG-DEMO-{1000 + index}",
                OrderStatus.Delivered))
            .ToArray();
        var orderService = new RecordingOrderService
        {
            CustomerOrdersResult = orders
        };
        var executor = CreateExecutor(orderService, maxOrderSearchResults: 5);

        ToolExecutionResult result = await executor.ExecuteAsync(
            OrderToolExecutor.GetCustomerOrdersToolName,
            ParseJson("""{"customerId":17}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, orderService.GetCustomerOrdersCallCount);
        Assert.Equal(17, orderService.LastCustomerId);
        Assert.Equal(5, orderService.LastMaxResults);
        AssertNoOtherServiceCalls(orderService, expectedCustomerOrdersCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement[] returnedOrders = document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(5, returnedOrders.Length);
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5 },
            returnedOrders.Select(item => item.GetProperty("id").GetInt32()));
        Assert.All(returnedOrders, item => AssertMinimizedOrderShape(item));
    }

    [Fact]
    public async Task ExecuteAsync_LatestCustomerOrder_RoutesToLatestService()
    {
        OrderDto order = CreateOrder(24, "AYG-DEMO-1024", OrderStatus.Preparing);
        var orderService = new RecordingOrderService
        {
            LatestOrderResult = order
        };
        var executor = CreateExecutor(orderService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            OrderToolExecutor.GetLatestCustomerOrderToolName,
            ParseJson("""{"customerId":11}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, orderService.GetLatestCallCount);
        Assert.Equal(11, orderService.LastCustomerId);
        AssertNoOtherServiceCalls(orderService, expectedLatestCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        AssertMinimizedOrder(
            document.RootElement.GetProperty("data"),
            order,
            "Haz\u0131rlan\u0131yor");
    }

    [Theory]
    [InlineData(OrderStatus.Pending, "Bekliyor")]
    [InlineData(OrderStatus.Preparing, "Haz\u0131rlan\u0131yor")]
    [InlineData(OrderStatus.Shipped, "Kargoya verildi")]
    [InlineData(OrderStatus.Delivered, "Teslim edildi")]
    [InlineData(OrderStatus.Cancelled, "\u0130ptal edildi")]
    [InlineData((OrderStatus)999, "Bilinmiyor")]
    public async Task ExecuteAsync_OrderStatus_UsesStableTurkishAgentText(
        OrderStatus status,
        string expectedText)
    {
        OrderDto order = CreateOrder(1, "AYG-DEMO-1001", status);
        var orderService = new RecordingOrderService
        {
            OrderByNumberResult = order
        };
        var executor = CreateExecutor(orderService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            OrderToolExecutor.GetOrderByNumberToolName,
            ParseJson("""{"orderNumber":"AYG-DEMO-1001"}"""));

        using JsonDocument document = JsonDocument.Parse(result.Content);
        Assert.Equal(
            expectedText,
            document.RootElement.GetProperty("data").GetProperty("status").GetString());
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public async Task ExecuteAsync_InvalidMalformedOrWrongTypeArguments_AreRejectedWithoutServiceCall(
        string toolName,
        string json)
    {
        var orderService = new RecordingOrderService();
        var executor = CreateExecutor(orderService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson(json));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoOtherServiceCalls(orderService);
    }

    [Theory]
    [InlineData(OrderToolExecutor.GetOrderByNumberToolName)]
    [InlineData(OrderToolExecutor.GetCustomerOrdersToolName)]
    [InlineData(OrderToolExecutor.GetLatestCustomerOrderToolName)]
    public async Task ExecuteAsync_UndefinedArguments_AreRejectedWithoutServiceCall(
        string toolName)
    {
        var orderService = new RecordingOrderService();
        var executor = CreateExecutor(orderService);

        ToolExecutionResult result = await executor.ExecuteAsync(toolName, default);

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoOtherServiceCalls(orderService);
    }

    [Theory]
    [InlineData("get_order_by_id")]
    [InlineData("get_all_orders")]
    [InlineData("create_order")]
    [InlineData("update_order")]
    [InlineData("delete_order")]
    [InlineData("export_orders")]
    [InlineData("execute_sql")]
    [InlineData("Get_Order_By_Number")]
    public async Task ExecuteAsync_UnknownWriteBulkOrServiceOnlyTool_IsRejectedWithoutServiceCall(
        string toolName)
    {
        var orderService = new RecordingOrderService();
        var executor = CreateExecutor(orderService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("{}"));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoOtherServiceCalls(orderService);
    }

    [Fact]
    public async Task ExecuteAsync_MissingOrderOrEmptyList_ReturnsExplicitNotFoundResults()
    {
        var orderService = new RecordingOrderService();
        var executor = CreateExecutor(orderService);

        ToolExecutionResult byNumber = await executor.ExecuteAsync(
            OrderToolExecutor.GetOrderByNumberToolName,
            ParseJson("""{"orderNumber":"AYG-DEMO-9999"}"""));
        ToolExecutionResult customerOrders = await executor.ExecuteAsync(
            OrderToolExecutor.GetCustomerOrdersToolName,
            ParseJson("""{"customerId":12}"""));
        ToolExecutionResult latest = await executor.ExecuteAsync(
            OrderToolExecutor.GetLatestCustomerOrderToolName,
            ParseJson("""{"customerId":12}"""));

        AssertNotFoundJson(byNumber);
        AssertNotFoundJson(customerOrders);
        AssertNotFoundJson(latest);
        AssertNoOtherServiceCalls(
            orderService,
            expectedNumberCalls: 1,
            expectedCustomerOrdersCalls: 1,
            expectedLatestCalls: 1);
    }

    public static IEnumerable<object[]> InvalidArgumentCases()
    {
        yield return [OrderToolExecutor.GetOrderByNumberToolName, "{}"];
        yield return [OrderToolExecutor.GetOrderByNumberToolName, "null"];
        yield return [OrderToolExecutor.GetOrderByNumberToolName, "[]"];
        yield return [OrderToolExecutor.GetOrderByNumberToolName, """{"orderNumber":null}"""];
        yield return [OrderToolExecutor.GetOrderByNumberToolName, """{"orderNumber":1001}"""];
        yield return [OrderToolExecutor.GetOrderByNumberToolName, """{"orderNumber":"   "}"""];
        yield return [OrderToolExecutor.GetOrderByNumberToolName, """{"OrderNumber":"AYG-DEMO-1001"}"""];
        yield return [OrderToolExecutor.GetOrderByNumberToolName, """{"orderNumber":"AYG-DEMO-1001","extra":true}"""];
        yield return
        [
            OrderToolExecutor.GetOrderByNumberToolName,
            JsonSerializer.Serialize(new
            {
                orderNumber = new string('A', CustomerOrder.MaximumOrderNumberLength + 1)
            })
        ];

        foreach (string toolName in new[]
        {
            OrderToolExecutor.GetCustomerOrdersToolName,
            OrderToolExecutor.GetLatestCustomerOrderToolName
        })
        {
            yield return [toolName, "{}"];
            yield return [toolName, "null"];
            yield return [toolName, "[]"];
            yield return [toolName, """{"customerId":null}"""];
            yield return [toolName, """{"customerId":"1"}"""];
            yield return [toolName, """{"customerId":0}"""];
            yield return [toolName, """{"customerId":-1}"""];
            yield return [toolName, """{"customerId":1.5}"""];
            yield return [toolName, """{"customerId":2147483648}"""];
            yield return [toolName, """{"CustomerId":1}"""];
            yield return [toolName, """{"customerId":1,"extra":true}"""];
        }
    }

    private static OrderToolExecutor CreateExecutor(
        RecordingOrderService orderService,
        int maxOrderSearchResults = 5)
    {
        return new OrderToolExecutor(
            orderService,
            Options.Create(new AgentOptions
            {
                MaxOrderSearchResults = maxOrderSearchResults
            }),
            new RecordingToolCallLogger());
    }

    private static OrderDto CreateOrder(
        int id,
        string orderNumber,
        OrderStatus status)
    {
        return new OrderDto(
            id,
            orderNumber,
            77,
            new DateTime(2026, 2, id, 10, 0, 0, DateTimeKind.Utc),
            status,
            100m + id);
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void AssertToolSchema(
        IReadOnlyList<OllamaToolDefinition> definitions,
        string toolName,
        string argumentName,
        string argumentType)
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
        Assert.Equal(argumentType, property.Value.Type);
        Assert.False(string.IsNullOrWhiteSpace(property.Value.Description));
    }

    private static void AssertMinimizedOrder(
        JsonElement data,
        OrderDto expected,
        string expectedStatus)
    {
        AssertMinimizedOrderShape(data);
        Assert.Equal(expected.Id, data.GetProperty("id").GetInt32());
        Assert.Equal(expected.OrderNumber, data.GetProperty("orderNumber").GetString());
        Assert.Equal(expected.OrderDate, data.GetProperty("orderDate").GetDateTime());
        Assert.Equal(expectedStatus, data.GetProperty("status").GetString());
        Assert.Equal(expected.TotalAmount, data.GetProperty("totalAmount").GetDecimal());
    }

    private static void AssertMinimizedOrderShape(JsonElement data)
    {
        Assert.Equal(5, data.EnumerateObject().Count());
        Assert.True(data.TryGetProperty("id", out _));
        Assert.True(data.TryGetProperty("orderNumber", out _));
        Assert.True(data.TryGetProperty("orderDate", out _));
        Assert.True(data.TryGetProperty("status", out _));
        Assert.True(data.TryGetProperty("totalAmount", out _));
        Assert.False(data.TryGetProperty("customerId", out _));
        Assert.False(data.TryGetProperty("customer", out _));
    }

    private static void AssertRejectedJson(string content)
    {
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("error").GetString()));
        Assert.False(root.TryGetProperty("data", out _));
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

    private static void AssertNoOtherServiceCalls(
        RecordingOrderService orderService,
        int expectedIdCalls = 0,
        int expectedNumberCalls = 0,
        int expectedCustomerOrdersCalls = 0,
        int expectedLatestCalls = 0)
    {
        Assert.Equal(expectedIdCalls, orderService.GetByIdCallCount);
        Assert.Equal(expectedNumberCalls, orderService.GetByNumberCallCount);
        Assert.Equal(expectedCustomerOrdersCalls, orderService.GetCustomerOrdersCallCount);
        Assert.Equal(expectedLatestCalls, orderService.GetLatestCallCount);
    }

    private sealed class RecordingOrderService : IOrderService
    {
        public OrderDto? OrderByIdResult { get; init; }

        public OrderDto? OrderByNumberResult { get; init; }

        public IReadOnlyList<OrderDto> CustomerOrdersResult { get; init; } = [];

        public OrderDto? LatestOrderResult { get; init; }

        public int GetByIdCallCount { get; private set; }

        public int GetByNumberCallCount { get; private set; }

        public int GetCustomerOrdersCallCount { get; private set; }

        public int GetLatestCallCount { get; private set; }

        public int? LastId { get; private set; }

        public string? LastOrderNumber { get; private set; }

        public int? LastCustomerId { get; private set; }

        public int? LastMaxResults { get; private set; }

        public Task<OrderDto?> GetOrderByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByIdCallCount++;
            LastId = id;
            return Task.FromResult(OrderByIdResult);
        }

        public Task<OrderDto?> GetOrderByNumberAsync(
            string orderNumber,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByNumberCallCount++;
            LastOrderNumber = orderNumber;
            return Task.FromResult(OrderByNumberResult);
        }

        public Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(
            int customerId,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCustomerOrdersCallCount++;
            LastCustomerId = customerId;
            LastMaxResults = maxResults;
            return Task.FromResult(CustomerOrdersResult);
        }

        public Task<OrderDto?> GetLatestCustomerOrderAsync(
            int customerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetLatestCallCount++;
            LastCustomerId = customerId;
            return Task.FromResult(LatestOrderResult);
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
