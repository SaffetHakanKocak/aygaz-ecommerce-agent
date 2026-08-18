using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class SalesAnalyticsToolExecutorTests
{
    [Fact]
    public void ToolDefinitions_ExposeExactlyThreeStrictReadOnlySalesTools()
    {
        var executor = CreateExecutor(new RecordingSalesAnalyticsService());

        Assert.Equal(3, executor.ToolDefinitions.Count);
        Assert.Equal(
            new[]
            {
                SalesAnalyticsToolExecutor.GetSalesSummaryToolName,
                SalesAnalyticsToolExecutor.GetTopSellingProductsToolName,
                SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName
            },
            executor.ToolDefinitions.Select(definition => definition.Function.Name));
        Assert.DoesNotContain(
            executor.ToolDefinitions,
            definition => definition.Function.Name is
                "get_all_sales" or "get_all_order_items" or "execute_sql");

        AssertToolSchema(
            executor.ToolDefinitions,
            SalesAnalyticsToolExecutor.GetSalesSummaryToolName,
            ("fromDate", "string"),
            ("toDate", "string"));
        AssertToolSchema(
            executor.ToolDefinitions,
            SalesAnalyticsToolExecutor.GetTopSellingProductsToolName,
            ("fromDate", "string"),
            ("toDate", "string"),
            ("limit", "integer"));
        AssertToolSchema(
            executor.ToolDefinitions,
            SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName,
            ("customerId", "integer"),
            ("fromDate", "string"),
            ("toDate", "string"));

        Assert.All(
            executor.ToolDefinitions,
            definition =>
            {
                Assert.Contains("2026-03-06", definition.Function.Description);
                Assert.False(definition.Function.Parameters.AdditionalProperties);
            });
    }

    [Fact]
    public async Task ExecuteAsync_ValidSalesSummary_RoutesDatesAndReturnsMinimumCurrencyResult()
    {
        var service = new RecordingSalesAnalyticsService
        {
            SalesSummaryResult = new SalesSummaryDto(14946.40m, 20, 80L, 747.32m)
        };
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            SalesAnalyticsToolExecutor.GetSalesSummaryToolName,
            ParseJson("""{"toDate":"2026-03-06","fromDate":"2026-02-01"}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, service.SummaryCallCount);
        Assert.Equal(new DateOnly(2026, 2, 1), service.LastFromDate);
        Assert.Equal(new DateOnly(2026, 3, 6), service.LastToDate);
        AssertNoServiceCallsExcept(service, expectedSummaryCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal(5, data.EnumerateObject().Count());
        Assert.Equal(14946.40m, data.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(20L, data.GetProperty("orderCount").GetInt64());
        Assert.Equal(80L, data.GetProperty("itemsSold").GetInt64());
        Assert.Equal(747.32m, data.GetProperty("averageOrderValue").GetDecimal());
        Assert.Equal("TRY", data.GetProperty("currencyCode").GetString());
        AssertNoRawSalesFields(data);
    }

    [Fact]
    public async Task ExecuteAsync_TopProducts_PassesLimitDefensivelyCapsAndMinimizesResults()
    {
        TopSellingProductDto[] products = Enumerable.Range(1, 7)
            .Select(index => new TopSellingProductDto(
                $"AYG-DEMO-PRD-{index:000}",
                $"Demo Product {index}",
                100L - index,
                1000m + index))
            .ToArray();
        var service = new RecordingSalesAnalyticsService
        {
            TopProductsResult = products
        };
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            SalesAnalyticsToolExecutor.GetTopSellingProductsToolName,
            ParseJson(
                """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":5}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, service.TopProductsCallCount);
        Assert.Equal(5, service.LastLimit);
        AssertNoServiceCallsExcept(service, expectedTopCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal(2, data.EnumerateObject().Count());
        Assert.Equal("TRY", data.GetProperty("currencyCode").GetString());
        JsonElement[] items = data.GetProperty("products").EnumerateArray().ToArray();
        Assert.Equal(5, items.Length);
        Assert.Equal(
            Enumerable.Range(1, 5).Select(index => $"AYG-DEMO-PRD-{index:000}"),
            items.Select(item => item.GetProperty("sku").GetString()));
        Assert.All(items, AssertMinimizedTopProductShape);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyTopProducts_ReturnsSuccessfulEmptyWrapper()
    {
        var service = new RecordingSalesAnalyticsService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            SalesAnalyticsToolExecutor.GetTopSellingProductsToolName,
            ParseJson(
                """{"fromDate":"2026-07-01","toDate":"2026-07-31","limit":5}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal("TRY", data.GetProperty("currencyCode").GetString());
        Assert.Empty(data.GetProperty("products").EnumerateArray());
        AssertNoServiceCallsExcept(service, expectedTopCalls: 1);
    }

    [Theory]
    [InlineData(3, 2941.00, 12)]
    [InlineData(0, 0.00, 0)]
    public async Task ExecuteAsync_CustomerSummary_RoutesAndReturnsMinimumResult(
        int orderCount,
        double totalSpent,
        long itemsPurchased)
    {
        var service = new RecordingSalesAnalyticsService
        {
            CustomerSummaryResult = new CustomerPurchaseSummaryDto(
                orderCount,
                (decimal)totalSpent,
                itemsPurchased)
        };
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName,
            ParseJson(
                """{"customerId":1,"fromDate":"2025-12-07","toDate":"2026-03-06"}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, service.CustomerSummaryCallCount);
        Assert.Equal(1, service.LastCustomerId);
        Assert.Equal(new DateOnly(2025, 12, 7), service.LastFromDate);
        Assert.Equal(new DateOnly(2026, 3, 6), service.LastToDate);
        AssertNoServiceCallsExcept(service, expectedCustomerCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal(4, data.EnumerateObject().Count());
        Assert.Equal(orderCount, data.GetProperty("orderCount").GetInt32());
        Assert.Equal((decimal)totalSpent, data.GetProperty("totalSpent").GetDecimal());
        Assert.Equal(itemsPurchased, data.GetProperty("itemsPurchased").GetInt64());
        Assert.Equal("TRY", data.GetProperty("currencyCode").GetString());
        AssertNoRawSalesFields(data);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCustomer_ReturnsExplicitNotFound()
    {
        var service = new RecordingSalesAnalyticsService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName,
            ParseJson(
                """{"customerId":404,"fromDate":"2026-02-01","toDate":"2026-03-06"}"""));

        AssertNotFoundJson(result);
        AssertNoServiceCallsExcept(service, expectedCustomerCalls: 1);
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public async Task ExecuteAsync_InvalidArguments_AreRejectedWithoutServiceCall(
        string toolName,
        string json)
    {
        var service = new RecordingSalesAnalyticsService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson(json));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Theory]
    [InlineData(SalesAnalyticsToolExecutor.GetSalesSummaryToolName)]
    [InlineData(SalesAnalyticsToolExecutor.GetTopSellingProductsToolName)]
    [InlineData(SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName)]
    public async Task ExecuteAsync_UndefinedArguments_AreRejectedWithoutServiceCall(
        string toolName)
    {
        var service = new RecordingSalesAnalyticsService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(toolName, default);

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Theory]
    [InlineData("get_all_sales")]
    [InlineData("get_all_order_items")]
    [InlineData("execute_sql")]
    [InlineData("run_query")]
    [InlineData("export_sales")]
    [InlineData("change_revenue")]
    [InlineData("create_order_item")]
    [InlineData("update_order_item")]
    [InlineData("delete_order_item")]
    [InlineData("change_order_total")]
    [InlineData("raw_sales_query")]
    [InlineData("Get_Sales_Summary")]
    public async Task ExecuteAsync_UnknownBulkRawOrWriteTool_IsRejectedWithoutServiceCall(
        string toolName)
    {
        var service = new RecordingSalesAnalyticsService();
        var executor = CreateExecutor(service);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("{}"));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoServiceCallsExcept(service);
    }

    [Theory]
    [MemberData(nameof(InvalidOptionCases))]
    public void Constructor_InvalidOptions_Throws(
        CommerceOptions options,
        Type expectedExceptionType)
    {
        Exception exception = Assert.IsAssignableFrom<Exception>(
            Record.Exception(
                () => new SalesAnalyticsToolExecutor(
                new RecordingSalesAnalyticsService(),
                Options.Create(options),
                    new RecordingToolCallLogger())));

        Assert.Equal(expectedExceptionType, exception.GetType());
    }

    public static IEnumerable<object[]> InvalidArgumentCases()
    {
        const string Summary = SalesAnalyticsToolExecutor.GetSalesSummaryToolName;
        const string Top = SalesAnalyticsToolExecutor.GetTopSellingProductsToolName;
        const string Customer =
            SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName;

        yield return [Summary, "{}"];
        yield return [Summary, "null"];
        yield return [Summary, "[]"];
        yield return [Summary, """{"fromDate":"2026-02-01"}"""];
        yield return [Summary, """{"fromDate":null,"toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":20260201,"toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":" 2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":"2026-02-01T00:00:00Z","toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":"01.02.2026","toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":"2026-02-29","toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":"2026-03-07","toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":"2025-03-05","toDate":"2026-03-06"}"""];
        yield return [Summary, """{"FromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Summary, """{"fromDate":"2026-02-01","toDate":"2026-03-06","extra":true}"""];
        yield return [Summary, """{"fromDate":"2026-02-01","fromDate":"2026-02-02","toDate":"2026-03-06"}"""];

        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":null}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":"5"}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":0}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":-1}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":1.5}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":11}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":2147483648}"""];
        yield return [Top, """{"fromDate":"2026-03-07","toDate":"2026-03-06","limit":5}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","Limit":5}"""];
        yield return [Top, """{"fromDate":"2026-02-01","toDate":"2026-03-06","limit":5,"extra":true}"""];

        yield return [Customer, """{"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":null,"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":"1","fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":0,"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":-1,"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":1.5,"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":2147483648,"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":1,"fromDate":"2026-03-07","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"CustomerId":1,"fromDate":"2026-02-01","toDate":"2026-03-06"}"""];
        yield return [Customer, """{"customerId":1,"fromDate":"2026-02-01","toDate":"2026-03-06","extra":true}"""];
    }

    public static IEnumerable<object[]> InvalidOptionCases()
    {
        yield return [CreateOptions(currencyCode: "try"), typeof(ArgumentException)];
        yield return [CreateOptions(currencyCode: "TR"), typeof(ArgumentException)];
        yield return [CreateOptions(currencyCode: "TR1"), typeof(ArgumentException)];
        yield return [CreateOptions(referenceDate: "2026-03-06T00:00:00Z"), typeof(ArgumentException)];
        yield return [CreateOptions(maximumRangeDays: 0), typeof(ArgumentOutOfRangeException)];
        yield return [CreateOptions(maximumRangeDays: 367), typeof(ArgumentOutOfRangeException)];
        yield return [CreateOptions(maximumTopProducts: 0), typeof(ArgumentOutOfRangeException)];
        yield return [CreateOptions(maximumTopProducts: 11), typeof(ArgumentOutOfRangeException)];
    }

    private static SalesAnalyticsToolExecutor CreateExecutor(
        RecordingSalesAnalyticsService service)
    {
        return new SalesAnalyticsToolExecutor(
            service,
            Options.Create(CreateOptions()),
            new RecordingToolCallLogger());
    }

    private static CommerceOptions CreateOptions(
        string currencyCode = "TRY",
        string referenceDate = "2026-03-06",
        int maximumRangeDays = 366,
        int maximumTopProducts = 10)
    {
        return new CommerceOptions
        {
            CurrencyCode = currencyCode,
            AnalyticsReferenceDate = referenceDate,
            MaximumAnalysisRangeDays = maximumRangeDays,
            MaximumTopProducts = maximumTopProducts
        };
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void AssertToolSchema(
        IReadOnlyList<OllamaToolDefinition> definitions,
        string toolName,
        params (string Name, string Type)[] arguments)
    {
        OllamaToolDefinition definition = Assert.Single(
            definitions,
            candidate => candidate.Function.Name == toolName);
        Assert.Equal("function", definition.Type);
        Assert.False(string.IsNullOrWhiteSpace(definition.Function.Description));
        Assert.Equal("object", definition.Function.Parameters.Type);
        Assert.False(definition.Function.Parameters.AdditionalProperties);
        Assert.Equal(
            arguments.Select(argument => argument.Name),
            definition.Function.Parameters.Required);
        Assert.Equal(arguments.Length, definition.Function.Parameters.Properties.Count);

        foreach ((string name, string type) in arguments)
        {
            OllamaToolProperty property =
                definition.Function.Parameters.Properties[name];
            Assert.Equal(type, property.Type);
            Assert.False(string.IsNullOrWhiteSpace(property.Description));
        }
    }

    private static void AssertMinimizedTopProductShape(JsonElement item)
    {
        Assert.Equal(4, item.EnumerateObject().Count());
        Assert.True(item.TryGetProperty("sku", out _));
        Assert.True(item.TryGetProperty("productName", out _));
        Assert.True(item.TryGetProperty("quantitySold", out _));
        Assert.True(item.TryGetProperty("revenue", out _));
        AssertNoRawSalesFields(item);
    }

    private static void AssertNoRawSalesFields(JsonElement data)
    {
        Assert.False(data.TryGetProperty("id", out _));
        Assert.False(data.TryGetProperty("customerId", out _));
        Assert.False(data.TryGetProperty("customerOrderId", out _));
        Assert.False(data.TryGetProperty("productId", out _));
        Assert.False(data.TryGetProperty("orderItems", out _));
        Assert.False(data.TryGetProperty("orders", out _));
        Assert.False(data.TryGetProperty("unitPrice", out _));
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
        RecordingSalesAnalyticsService service,
        int expectedSummaryCalls = 0,
        int expectedTopCalls = 0,
        int expectedCustomerCalls = 0)
    {
        Assert.Equal(expectedSummaryCalls, service.SummaryCallCount);
        Assert.Equal(expectedTopCalls, service.TopProductsCallCount);
        Assert.Equal(expectedCustomerCalls, service.CustomerSummaryCallCount);
    }

    private sealed class RecordingSalesAnalyticsService : ISalesAnalyticsService
    {
        public SalesSummaryDto SalesSummaryResult { get; init; } =
            new(0m, 0, 0L, 0m);

        public IReadOnlyList<TopSellingProductDto> TopProductsResult { get; init; } = [];

        public CustomerPurchaseSummaryDto? CustomerSummaryResult { get; init; }

        public int SummaryCallCount { get; private set; }

        public int TopProductsCallCount { get; private set; }

        public int CustomerSummaryCallCount { get; private set; }

        public DateOnly? LastFromDate { get; private set; }

        public DateOnly? LastToDate { get; private set; }

        public int? LastLimit { get; private set; }

        public int? LastCustomerId { get; private set; }

        public Task<SalesSummaryDto> GetSalesSummaryAsync(
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SummaryCallCount++;
            LastFromDate = fromDate;
            LastToDate = toDate;
            return Task.FromResult(SalesSummaryResult);
        }

        public Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(
            DateOnly fromDate,
            DateOnly toDate,
            int limit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TopProductsCallCount++;
            LastFromDate = fromDate;
            LastToDate = toDate;
            LastLimit = limit;
            return Task.FromResult(TopProductsResult);
        }

        public Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(
            int customerId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CustomerSummaryCallCount++;
            LastCustomerId = customerId;
            LastFromDate = fromDate;
            LastToDate = toDate;
            return Task.FromResult(CustomerSummaryResult);
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
