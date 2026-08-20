using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class CustomerToolExecutorTests
{
    [Fact]
    public void ToolDefinitions_ExposeExactlyTheThreeAllowedCustomerTools()
    {
        var executor = CreateExecutor(new RecordingCustomerService());

        IReadOnlyList<Aygaz.ECommerce.Agent.Models.OllamaToolDefinition> definitions =
            executor.ToolDefinitions;

        Assert.Equal(3, definitions.Count);
        Assert.Equal(
            new[]
            {
                CustomerToolExecutor.GetCustomerByEmailToolName,
                CustomerToolExecutor.GetCustomerByIdToolName,
                CustomerToolExecutor.SearchCustomersByNameToolName
            }.OrderBy(name => name),
            definitions.Select(definition => definition.Function.Name).OrderBy(name => name));
        Assert.DoesNotContain(
            definitions,
            definition => definition.Function.Name == "get_all_customers");

        AssertToolSchema(
            definitions,
            CustomerToolExecutor.GetCustomerByEmailToolName,
            "email",
            "string");
        AssertToolSchema(
            definitions,
            CustomerToolExecutor.GetCustomerByIdToolName,
            "id",
            "integer");
        AssertToolSchema(
            definitions,
            CustomerToolExecutor.SearchCustomersByNameToolName,
            "query",
            "string");
    }

    [Fact]
    public async Task ExecuteAsync_ValidEmail_RoutesToEmailServiceAndMinimizesResult()
    {
        CustomerDto customer = CreateCustomer(7, "Ahmet", "Yılmaz");
        var customerService = new RecordingCustomerService
        {
            CustomerByEmailResult = customer
        };
        var executor = CreateExecutor(customerService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            CustomerToolExecutor.GetCustomerByEmailToolName,
            ParseJson("""{"email":"  ahmet.yilmaz@example.com  "}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, customerService.GetByEmailCallCount);
        Assert.Equal("ahmet.yilmaz@example.com", customerService.LastEmail);
        AssertNoOtherServiceCalls(customerService, expectedEmailCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("found").GetBoolean());
        AssertMinimizedCustomer(root.GetProperty("data"), customer);
    }

    [Fact]
    public async Task ExecuteAsync_ValidId_RoutesToIdService()
    {
        CustomerDto customer = CreateCustomer(11, "Ece", "Öztürk");
        var customerService = new RecordingCustomerService
        {
            CustomerByIdResult = customer
        };
        var executor = CreateExecutor(customerService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            CustomerToolExecutor.GetCustomerByIdToolName,
            ParseJson("""{"id":11}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, customerService.GetByIdCallCount);
        Assert.Equal(11, customerService.LastId);
        AssertNoOtherServiceCalls(customerService, expectedIdCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        AssertMinimizedCustomer(document.RootElement.GetProperty("data"), customer);
    }

    [Fact]
    public async Task ExecuteAsync_NameSearch_LimitsAndMinimizesResults()
    {
        CustomerDto[] customers = Enumerable.Range(1, 7)
            .Select(index => CreateCustomer(index, $"Ad{index}", $"Soyad{index}"))
            .ToArray();
        var customerService = new RecordingCustomerService
        {
            NameSearchResult = customers
        };
        var executor = CreateExecutor(customerService, maxNameSearchResults: 5);

        ToolExecutionResult result = await executor.ExecuteAsync(
            CustomerToolExecutor.SearchCustomersByNameToolName,
            ParseJson("""{"query":"  Ahmet Yılmaz  "}"""));

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal(1, customerService.SearchByNameCallCount);
        Assert.Equal("Ahmet Yılmaz", customerService.LastNameQuery);
        AssertNoOtherServiceCalls(customerService, expectedNameCalls: 1);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement.ArrayEnumerator data = document.RootElement
            .GetProperty("data")
            .EnumerateArray();
        JsonElement[] returnedCustomers = data.ToArray();

        Assert.Equal(5, returnedCustomers.Length);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, returnedCustomers
            .Select(item => item.GetProperty("id").GetInt32()));

        foreach (JsonElement returnedCustomer in returnedCustomers)
        {
            Assert.Equal(7, returnedCustomer.EnumerateObject().Count());
            Assert.True(returnedCustomer.TryGetProperty("phoneNumber", out _));
            Assert.True(returnedCustomer.TryGetProperty("address", out _));
            Assert.False(returnedCustomer.TryGetProperty("phone", out _));
            Assert.False(returnedCustomer.TryGetProperty("createdAt", out _));
        }
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public async Task ExecuteAsync_InvalidOrWrongTypeArguments_AreRejectedWithoutServiceCall(
        string toolName,
        string json)
    {
        var customerService = new RecordingCustomerService();
        var executor = CreateExecutor(customerService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson(json));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoOtherServiceCalls(customerService);
    }

    [Theory]
    [InlineData(CustomerToolExecutor.GetCustomerByEmailToolName)]
    [InlineData(CustomerToolExecutor.GetCustomerByIdToolName)]
    [InlineData(CustomerToolExecutor.SearchCustomersByNameToolName)]
    public async Task ExecuteAsync_UndefinedArguments_AreRejectedWithoutServiceCall(
        string toolName)
    {
        var customerService = new RecordingCustomerService();
        var executor = CreateExecutor(customerService);

        ToolExecutionResult result = await executor.ExecuteAsync(toolName, default);

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoOtherServiceCalls(customerService);
    }

    [Fact]
    public async Task ExecuteAsync_OverlongEmailAndName_AreRejectedWithoutServiceCall()
    {
        var customerService = new RecordingCustomerService();
        var executor = CreateExecutor(customerService);
        string longEmailJson = JsonSerializer.Serialize(
            new { email = $"{new string('a', 255)}@example.com" });
        string longNameJson = JsonSerializer.Serialize(
            new { query = new string('a', 101) });

        ToolExecutionResult emailResult = await executor.ExecuteAsync(
            CustomerToolExecutor.GetCustomerByEmailToolName,
            ParseJson(longEmailJson));
        ToolExecutionResult nameResult = await executor.ExecuteAsync(
            CustomerToolExecutor.SearchCustomersByNameToolName,
            ParseJson(longNameJson));

        Assert.Equal(ToolExecutionStatus.Rejected, emailResult.Status);
        Assert.Equal(ToolExecutionStatus.Rejected, nameResult.Status);
        AssertNoOtherServiceCalls(customerService);
    }

    [Theory]
    [InlineData("get_all_customers")]
    [InlineData("delete_customer")]
    [InlineData("execute_sql")]
    [InlineData("drop_database")]
    [InlineData("Get_Customer_By_Email")]
    public async Task ExecuteAsync_UnknownOrDisallowedTool_IsRejectedWithoutServiceCall(
        string toolName)
    {
        var customerService = new RecordingCustomerService();
        var executor = CreateExecutor(customerService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            toolName,
            ParseJson("{}"));

        Assert.Equal(ToolExecutionStatus.Rejected, result.Status);
        AssertRejectedJson(result.Content);
        AssertNoOtherServiceCalls(customerService);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCustomer_ReturnsExplicitNotFoundResult()
    {
        var customerService = new RecordingCustomerService();
        var executor = CreateExecutor(customerService);

        ToolExecutionResult result = await executor.ExecuteAsync(
            CustomerToolExecutor.GetCustomerByEmailToolName,
            ParseJson("""{"email":"nobody@example.com"}"""));

        Assert.Equal(ToolExecutionStatus.NotFound, result.Status);
        Assert.Equal(1, customerService.GetByEmailCallCount);

        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(root.GetProperty("found").GetBoolean());
        Assert.False(root.TryGetProperty("data", out _));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }

    public static IEnumerable<object[]> InvalidArgumentCases()
    {
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, "{}"];
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, "null"];
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, "[]"];
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, """{"email":null}"""];
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, """{"email":42}"""];
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, """{"email":" "}"""];
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, """{"email":"not-an-email"}"""];
        yield return [CustomerToolExecutor.GetCustomerByEmailToolName, """{"email":"a@example.com","extra":true}"""];

        yield return [CustomerToolExecutor.GetCustomerByIdToolName, "{}"];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, "null"];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, """{"id":null}"""];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, """{"id":"1"}"""];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, """{"id":0}"""];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, """{"id":-1}"""];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, """{"id":1.5}"""];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, """{"id":2147483648}"""];
        yield return [CustomerToolExecutor.GetCustomerByIdToolName, """{"id":1,"extra":true}"""];

        yield return [CustomerToolExecutor.SearchCustomersByNameToolName, "{}"];
        yield return [CustomerToolExecutor.SearchCustomersByNameToolName, "null"];
        yield return [CustomerToolExecutor.SearchCustomersByNameToolName, """{"query":null}"""];
        yield return [CustomerToolExecutor.SearchCustomersByNameToolName, """{"query":123}"""];
        yield return [CustomerToolExecutor.SearchCustomersByNameToolName, """{"query":"   "}"""];
        yield return [CustomerToolExecutor.SearchCustomersByNameToolName, """{"query":"Ahmet","extra":true}"""];
    }

    private static CustomerToolExecutor CreateExecutor(
        RecordingCustomerService customerService,
        int maxNameSearchResults = 5)
    {
        return new CustomerToolExecutor(
            customerService,
            Options.Create(new AgentOptions
            {
                MaxNameSearchResults = maxNameSearchResults
            }),
            new RecordingToolCallLogger());
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static CustomerDto CreateCustomer(int id, string firstName, string lastName)
    {
        return new CustomerDto(
            id,
            firstName,
            lastName,
            $"customer{id}@example.com",
            $"000-000-{id:0000} (TEST)",
            "Atatürk Mah. Örnek Sok. No: 10",
            "İstanbul",
            new DateTime(2026, 1, id, 9, 0, 0, DateTimeKind.Utc));
    }

    private static void AssertToolSchema(
        IReadOnlyList<Aygaz.ECommerce.Agent.Models.OllamaToolDefinition> definitions,
        string toolName,
        string argumentName,
        string argumentType)
    {
        Aygaz.ECommerce.Agent.Models.OllamaToolDefinition definition = Assert.Single(
            definitions,
            candidate => candidate.Function.Name == toolName);

        Assert.Equal("function", definition.Type);
        Assert.False(string.IsNullOrWhiteSpace(definition.Function.Description));
        Assert.Equal("object", definition.Function.Parameters.Type);
        Assert.False(definition.Function.Parameters.AdditionalProperties);
        Assert.Equal(new[] { argumentName }, definition.Function.Parameters.Required);
        KeyValuePair<string, Aygaz.ECommerce.Agent.Models.OllamaToolProperty> property =
            Assert.Single(definition.Function.Parameters.Properties);
        Assert.Equal(argumentName, property.Key);
        Assert.Equal(argumentType, property.Value.Type);
        Assert.False(string.IsNullOrWhiteSpace(property.Value.Description));
    }

    private static void AssertMinimizedCustomer(JsonElement data, CustomerDto expected)
    {
        Assert.Equal(7, data.EnumerateObject().Count());
        Assert.Equal(expected.Id, data.GetProperty("id").GetInt32());
        Assert.Equal(expected.FirstName, data.GetProperty("firstName").GetString());
        Assert.Equal(expected.LastName, data.GetProperty("lastName").GetString());
        Assert.Equal(expected.Email, data.GetProperty("email").GetString());
        Assert.Equal(expected.City, data.GetProperty("city").GetString());
        Assert.Equal(expected.Phone, data.GetProperty("phoneNumber").GetString());
        Assert.Equal(expected.Address, data.GetProperty("address").GetString());
        Assert.False(data.TryGetProperty("phone", out _));
        Assert.False(data.TryGetProperty("createdAt", out _));
    }

    private static void AssertRejectedJson(string content)
    {
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("error").GetString()));
        Assert.False(root.TryGetProperty("data", out _));
    }

    private static void AssertNoOtherServiceCalls(
        RecordingCustomerService customerService,
        int expectedEmailCalls = 0,
        int expectedIdCalls = 0,
        int expectedNameCalls = 0)
    {
        Assert.Equal(expectedEmailCalls, customerService.GetByEmailCallCount);
        Assert.Equal(expectedIdCalls, customerService.GetByIdCallCount);
        Assert.Equal(expectedNameCalls, customerService.SearchByNameCallCount);
        Assert.Equal(0, customerService.GetAllCallCount);
    }

    private sealed class RecordingCustomerService : ICustomerService
    {
        public CustomerDto? CustomerByIdResult { get; init; }

        public CustomerDto? CustomerByEmailResult { get; init; }

        public IReadOnlyList<CustomerDto> NameSearchResult { get; init; } = [];

        public int GetByIdCallCount { get; private set; }

        public int GetByEmailCallCount { get; private set; }

        public int SearchByNameCallCount { get; private set; }

        public int GetAllCallCount { get; private set; }

        public int? LastId { get; private set; }

        public string? LastEmail { get; private set; }

        public string? LastNameQuery { get; private set; }

        public Task<CustomerDto?> GetCustomerByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            LastId = id;
            return Task.FromResult(CustomerByIdResult);
        }

        public Task<CustomerDto?> GetCustomerByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            GetByEmailCallCount++;
            LastEmail = email;
            return Task.FromResult(CustomerByEmailResult);
        }

        public Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(
            string searchTerm,
            CancellationToken cancellationToken = default)
        {
            SearchByNameCallCount++;
            LastNameQuery = searchTerm;
            return Task.FromResult(NameSearchResult);
        }

        public Task<IReadOnlyList<CustomerDto>> SearchCustomersByCityAsync(
            string city,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CustomerDto>>([]);
        }

        public Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(
            CancellationToken cancellationToken = default)
        {
            GetAllCallCount++;
            return Task.FromResult<IReadOnlyList<CustomerDto>>([]);
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
