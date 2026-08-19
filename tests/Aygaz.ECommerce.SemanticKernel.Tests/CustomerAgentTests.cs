#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Aygaz.ECommerce.SemanticKernel.Formatting;
using Aygaz.ECommerce.SemanticKernel.Plugins;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class CustomerPluginTests
{
    [Fact]
    public async Task GetCustomerByEmail_CallsExistingCustomerService()
    {
        var service = new RecordingCustomerService
        {
            CustomerByEmail = CreateCustomer(7, "Ahmet", "Yılmaz", "ahmet.yilmaz@example.com")
        };
        var plugin = new CustomerPlugin(service);

        CustomerAgentResult? result = await plugin.GetCustomerByEmailAsync("  ahmet.yilmaz@example.com  ");

        Assert.Equal(1, service.GetByEmailCallCount);
        Assert.Equal("ahmet.yilmaz@example.com", service.LastEmail);
        Assert.Equal(0, service.GetByIdCallCount);
        Assert.Equal(0, service.SearchByNameCallCount);
        Assert.Equal(0, service.GetAllCallCount);
        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("Ahmet", result.FirstName);
        Assert.Equal("Yılmaz", result.LastName);
        Assert.Equal("ahmet.yilmaz@example.com", result.Email);
        Assert.Equal("İstanbul", result.City);
    }

    [Fact]
    public async Task GetCustomerByEmail_ReturnsMinimizedDto()
    {
        var service = new RecordingCustomerService
        {
            CustomerByEmail = CreateCustomer(7, "Ahmet", "Yılmaz", "ahmet.yilmaz@example.com")
        };
        var plugin = new CustomerPlugin(service);

        CustomerAgentResult? result = await plugin.GetCustomerByEmailAsync("ahmet.yilmaz@example.com");

        Assert.NotNull(result);
        Assert.Null(result.GetType().GetProperty("Phone"));
        Assert.Null(result.GetType().GetProperty("CreatedAt"));
    }

    [Fact]
    public async Task SearchByName_AppliesMaxResultLimit()
    {
        var service = new RecordingCustomerService
        {
            SearchResults =
            [
                CreateCustomer(1, "Ahmet", "Yılmaz", "a1@example.com"),
                CreateCustomer(2, "Ahmet", "Demir", "a2@example.com"),
                CreateCustomer(3, "Ahmet", "Kaya", "a3@example.com")
            ]
        };
        var plugin = new CustomerPlugin(service, maxNameSearchResults: 2);

        IReadOnlyList<CustomerAgentResult> results =
            await plugin.SearchCustomersByNameAsync("Ahmet");

        Assert.Equal(1, service.SearchByNameCallCount);
        Assert.Equal("Ahmet", service.LastSearchTerm);
        Assert.Equal(0, service.GetAllCallCount);
        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal(2, results[1].Id);
    }

    [Fact]
    public async Task SearchByName_UnsupportedBulkQuery_DoesNotCallCustomerService()
    {
        var service = new RecordingCustomerService
        {
            SearchResults = [CreateCustomer(1, "Ahmet", "Yılmaz", "a1@example.com")]
        };
        var plugin = new CustomerPlugin(service);

        IReadOnlyList<CustomerAgentResult> results =
            await plugin.SearchCustomersByNameAsync("müşterileri getir");

        Assert.Empty(results);
        Assert.Equal(0, service.SearchByNameCallCount);
        Assert.Equal(0, service.GetByEmailCallCount);
        Assert.Equal(0, service.GetByIdCallCount);
        Assert.Equal(0, service.GetAllCallCount);
    }

    [Fact]
    public void Plugin_DoesNotExposeGetAllCustomers()
    {
        var plugin = new CustomerPlugin(new RecordingCustomerService());
        var functions = Microsoft.SemanticKernel.KernelPluginFactory
            .CreateFromObject(plugin)
            .Select(function => function.Name)
            .ToArray();

        Assert.Equal(3, functions.Length);
        Assert.Contains("get_customer_by_email", functions);
        Assert.Contains("get_customer_by_id", functions);
        Assert.Contains("search_customers_by_name", functions);
        Assert.DoesNotContain("get_all_customers", functions);
    }

    [Fact]
    public void Plugin_ExposesExactlyThreeKernelFunctions()
    {
        var plugin = Microsoft.SemanticKernel.KernelPluginFactory.CreateFromObject(
            new CustomerPlugin(new RecordingCustomerService()));

        Assert.Equal(3, plugin.FunctionCount);
        Assert.Equal(3, plugin.Select(function => function.Name).Distinct().Count());
    }

    [Fact]
    public void GetCustomerByEmail_MetadataIsSpecific()
    {
        var function = GetFunction("get_customer_by_email");

        Assert.Contains("email", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bulk", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", function.Metadata.Parameters[0].Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("get_all_customers", function.Name);
    }

    [Fact]
    public void GetCustomerById_MetadataIsSpecific()
    {
        var function = GetFunction("get_customer_by_id");

        Assert.Contains("customer id", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer number", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id", function.Metadata.Parameters[0].Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchCustomersByName_MetadataIsSpecific()
    {
        var function = GetFunction("search_customers_by_name");

        Assert.Contains("name", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bulk", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email", function.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", function.Metadata.Parameters[0].Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plugin_DoesNotExposeGetAllCustomers_ByName()
    {
        var names = Microsoft.SemanticKernel.KernelPluginFactory
            .CreateFromObject(new CustomerPlugin(new RecordingCustomerService()))
            .Select(function => function.Name);

        Assert.DoesNotContain("get_all_customers", names);
    }

    private static Microsoft.SemanticKernel.KernelFunction GetFunction(string name)
    {
        return Microsoft.SemanticKernel.KernelPluginFactory.CreateFromObject(
            new CustomerPlugin(new RecordingCustomerService()))[name];
    }

    private static CustomerDto CreateCustomer(int id, string first, string last, string email)
    {
        return new CustomerDto(id, first, last, email, "555", "İstanbul", DateTime.UtcNow);
    }
}

public sealed class CustomerAgentRegistrationTests
{
    [Fact]
    public void Register_MapsCustomerRoute_AndIsolatesPlugin()
    {
        var options = new SemanticKernelOptions
        {
            ModelId = "dummy",
            Endpoint = "http://localhost:11434"
        };
        var registry = new AgentRegistry();
        var router = new AgentRouter();
        var registrar = new SemanticKernelAgentRegistrar(
            new SemanticKernelFactory(options),
            new SemanticKernelAgentFactory(),
            registry,
            router);

        var agent = CustomerAgentRegistration.Register(registrar, new RecordingCustomerService());

        Assert.Equal(CustomerAgentRegistration.AgentName, agent.Name);
        Assert.Equal(CustomerAgentRegistration.AgentName, router.ResolveAgentName("customer"));
        Assert.Same(agent, registry.GetAgent("CustomerAgent"));

        var functionNames = agent.Kernel.Plugins
            .SelectMany(plugin => plugin)
            .Select(function => function.Name)
            .ToArray();

        Assert.Single(agent.Kernel.Plugins);
        Assert.Equal(3, functionNames.Length);
        Assert.Contains("get_customer_by_email", functionNames);
        Assert.Contains("get_customer_by_id", functionNames);
        Assert.Contains("search_customers_by_name", functionNames);
        Assert.DoesNotContain("get_all_customers", functionNames);
        Assert.DoesNotContain("get_system_name", functionNames);
        Assert.DoesNotContain("add_numbers", functionNames);
        Assert.Contains(
            agent.Kernel.AutoFunctionInvocationFilters,
            filter => filter is CustomerExactLookupTerminationFilter);
    }
}

public sealed class CustomerLookupResponseFormatterTests
{
    [Fact]
    public void FormatsSingleCustomer()
    {
        var customer = new CustomerAgentResult(1, "Ahmet", "Yılmaz", "ahmet.yilmaz@example.com", "İstanbul");

        bool formatted = CustomerLookupResponseFormatter.TryFormatExactLookup(
            "get_customer_by_email",
            customer,
            out string text);

        Assert.True(formatted);
        Assert.Equal("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).", text);
    }

    [Fact]
    public void FormatsMissingCustomerWithoutInventingData()
    {
        bool formatted = CustomerLookupResponseFormatter.TryFormatExactLookup(
            "get_customer_by_id",
            null,
            out string text);

        Assert.True(formatted);
        Assert.Equal("Bu bilgilerle kayıtlı müşteri bulunamadı.", text);
    }

    [Fact]
    public void SearchByName_SingleResult_UsesFastPath()
    {
        IReadOnlyList<CustomerAgentResult> results =
        [
            new CustomerAgentResult(1, "Ahmet", "Yılmaz", "ahmet.yilmaz@example.com", "İstanbul")
        ];

        bool formatted = CustomerLookupResponseFormatter.TryFormatExactLookup(
            "search_customers_by_name",
            results,
            out string text);

        Assert.True(formatted);
        Assert.Equal("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).", text);
    }

    [Fact]
    public void SearchByName_MultipleResults_DoesNotUseFastPath()
    {
        IReadOnlyList<CustomerAgentResult> results =
        [
            new CustomerAgentResult(1, "Ahmet", "Yılmaz", "a1@example.com", "İstanbul"),
            new CustomerAgentResult(2, "Ahmet", "Demir", "a2@example.com", "Ankara")
        ];

        bool formatted = CustomerLookupResponseFormatter.TryFormatExactLookup(
            "search_customers_by_name",
            results,
            out string text);

        Assert.False(formatted);
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void SearchByName_ZeroResults_DoesNotUseFastPath()
    {
        IReadOnlyList<CustomerAgentResult> results = [];

        bool formatted = CustomerLookupResponseFormatter.TryFormatExactLookup(
            "search_customers_by_name",
            results,
            out string text);

        Assert.False(formatted);
        Assert.Equal(string.Empty, text);
    }
}

internal sealed class RecordingCustomerService : ICustomerService
{
    public CustomerDto? CustomerByEmail { get; init; }
    public CustomerDto? CustomerById { get; init; }
    public IReadOnlyList<CustomerDto> SearchResults { get; init; } = [];

    public int GetByEmailCallCount { get; private set; }
    public int GetByIdCallCount { get; private set; }
    public int SearchByNameCallCount { get; private set; }
    public int GetAllCallCount { get; private set; }

    public string? LastEmail { get; private set; }
    public int LastId { get; private set; }
    public string? LastSearchTerm { get; private set; }

    public Task<CustomerDto?> GetCustomerByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        LastId = id;
        return Task.FromResult(CustomerById);
    }

    public Task<CustomerDto?> GetCustomerByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        GetByEmailCallCount++;
        LastEmail = email;
        return Task.FromResult(CustomerByEmail);
    }

    public Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        SearchByNameCallCount++;
        LastSearchTerm = searchTerm;
        return Task.FromResult(SearchResults);
    }

    public Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(CancellationToken cancellationToken = default)
    {
        GetAllCallCount++;
        return Task.FromResult<IReadOnlyList<CustomerDto>>([]);
    }
}
