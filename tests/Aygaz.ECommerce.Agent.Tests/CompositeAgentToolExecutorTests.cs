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

    [Fact]
    public void ToolDefinitions_ExposeExactUnionOfSixCustomerAndOrderTools()
    {
        var customerModule = new RecordingToolModule(CustomerToolNames);
        var orderModule = new RecordingToolModule(OrderToolNames);
        var executor = new CompositeAgentToolExecutor(
            [customerModule, orderModule],
            new RecordingToolCallLogger());

        Assert.Equal(6, executor.ToolDefinitions.Count);
        Assert.Equal(
            CustomerToolNames.Concat(OrderToolNames),
            executor.ToolDefinitions.Select(definition => definition.Function.Name));
        Assert.Equal(
            6,
            executor.ToolDefinitions
                .Select(definition => definition.Function.Name)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public async Task ExecuteAsync_KnownCustomerAndOrderTools_DispatchOnlyToOwningModule()
    {
        ToolExecutionResult customerResult =
            ToolExecutionResult.FromSuccess(new { id = 1 });
        ToolExecutionResult orderResult =
            ToolExecutionResult.FromSuccess(new { orderNumber = "AYG-DEMO-1004" });
        var customerModule = new RecordingToolModule(
            CustomerToolNames,
            customerResult);
        var orderModule = new RecordingToolModule(
            OrderToolNames,
            orderResult);
        var executor = new CompositeAgentToolExecutor(
            [customerModule, orderModule],
            new RecordingToolCallLogger());
        JsonElement customerArguments = ParseJson(
            """{"email":"ahmet.yilmaz@example.com"}""");
        JsonElement orderArguments = ParseJson("""{"customerId":1}""");

        ToolExecutionResult actualCustomerResult = await executor.ExecuteAsync(
            CustomerToolExecutor.GetCustomerByEmailToolName,
            customerArguments);
        ToolExecutionResult actualOrderResult = await executor.ExecuteAsync(
            OrderToolExecutor.GetLatestCustomerOrderToolName,
            orderArguments);

        Assert.Same(customerResult, actualCustomerResult);
        Assert.Same(orderResult, actualOrderResult);
        ToolInvocation customerCall = Assert.Single(customerModule.Calls);
        Assert.Equal(CustomerToolExecutor.GetCustomerByEmailToolName, customerCall.ToolName);
        Assert.Equal(
            "ahmet.yilmaz@example.com",
            customerCall.Arguments.GetProperty("email").GetString());
        ToolInvocation orderCall = Assert.Single(orderModule.Calls);
        Assert.Equal(OrderToolExecutor.GetLatestCustomerOrderToolName, orderCall.ToolName);
        Assert.Equal(1, orderCall.Arguments.GetProperty("customerId").GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("get_all_orders")]
    [InlineData("create_order")]
    [InlineData("delete_order")]
    [InlineData("execute_sql")]
    [InlineData("Get_Order_By_Number")]
    public async Task ExecuteAsync_UnknownWriteBulkOrSqlTool_IsRejectedWithoutModuleCall(
        string? toolName)
    {
        var customerModule = new RecordingToolModule(CustomerToolNames);
        var orderModule = new RecordingToolModule(OrderToolNames);
        var logger = new RecordingToolCallLogger();
        var executor = new CompositeAgentToolExecutor(
            [customerModule, orderModule],
            logger);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("{}"));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        Assert.Empty(customerModule.Calls);
        Assert.Empty(orderModule.Calls);
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
