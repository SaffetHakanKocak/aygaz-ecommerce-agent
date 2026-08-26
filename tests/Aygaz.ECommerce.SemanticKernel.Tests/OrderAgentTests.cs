#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Aygaz.ECommerce.SemanticKernel.Formatting;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Aygaz.ECommerce.SemanticKernel.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class OrderPluginTests
{
    [Fact]
    public async Task GetOrderByNumber_CallsExistingOrderService()
    {
        var service = new RecordingOrderService
        {
            OrderByNumber = CreateOrder("AYG-DEMO-1004", OrderStatus.Preparing)
        };
        var plugin = new OrderPlugin(service);

        OrderAgentResult? result = await plugin.GetOrderByNumberAsync("  AYG-DEMO-1004  ");

        Assert.Equal(1, service.GetByNumberCallCount);
        Assert.Equal("AYG-DEMO-1004", service.LastOrderNumber);
        Assert.NotNull(result);
        Assert.Equal("AYG-DEMO-1004", result.OrderNumber);
        Assert.Equal("Hazırlanıyor", result.Status);
    }

    [Fact]
    public async Task GetCustomerOrders_CallsExistingOrderService()
    {
        var service = new RecordingOrderService
        {
            CustomerOrders =
            [
                CreateOrder("AYG-DEMO-1001", OrderStatus.Delivered),
                CreateOrder("AYG-DEMO-1002", OrderStatus.Cancelled)
            ]
        };
        var plugin = new OrderPlugin(service);

        IReadOnlyList<OrderAgentResult> results = await plugin.GetCustomerOrdersAsync(1);

        Assert.Equal(1, service.GetCustomerOrdersCallCount);
        Assert.Equal(1, service.LastCustomerId);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetLatestCustomerOrder_CallsExistingOrderService()
    {
        var service = new RecordingOrderService
        {
            LatestOrder = CreateOrder("AYG-DEMO-1004", OrderStatus.Preparing)
        };
        var plugin = new OrderPlugin(service);

        OrderAgentResult? result = await plugin.GetLatestCustomerOrderAsync(1);

        Assert.Equal(1, service.GetLatestCustomerOrderCallCount);
        Assert.Equal(1, service.LastCustomerId);
        Assert.NotNull(result);
        Assert.Equal("AYG-DEMO-1004", result.OrderNumber);
    }

    [Fact]
    public void Plugin_DoesNotExposeWriteOrBulkFunctions()
    {
        var functions = Microsoft.SemanticKernel.KernelPluginFactory
            .CreateFromObject(new OrderPlugin(new RecordingOrderService()))
            .Select(function => function.Name)
            .ToArray();

        Assert.Equal(3, functions.Length);
        Assert.Contains("get_order_by_number", functions);
        Assert.Contains("get_customer_orders", functions);
        Assert.Contains("get_latest_customer_order", functions);
        Assert.DoesNotContain("get_all_orders", functions);
        Assert.DoesNotContain("create_order", functions);
        Assert.DoesNotContain("update_order", functions);
        Assert.DoesNotContain("cancel_order", functions);
    }

    [Fact]
    public void OrderResult_UsesMinimalDto()
    {
        var result = new OrderAgentResult(1, "AYG-DEMO-1004", DateTime.UtcNow, "Hazırlanıyor", 910.75m);

        Assert.NotNull(result.GetType().GetProperty(nameof(OrderAgentResult.OrderNumber)));
        Assert.NotNull(result.GetType().GetProperty(nameof(OrderAgentResult.Status)));
        Assert.Null(result.GetType().GetProperty("CustomerId"));
    }

    private static OrderDto CreateOrder(string number, OrderStatus status)
    {
        return new OrderDto(1, number, 1, DateTime.UtcNow, status, 910.75m);
    }
}

public sealed class OrderAgentRegistrationTests
{
    [Fact]
    public void Register_MapsOrderRoute_AndIsolatesPlugin()
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

        var agent = OrderAgentRegistration.Register(registrar, new RecordingOrderService());

        Assert.Equal(OrderAgentRegistration.AgentName, agent.Name);
        Assert.Equal(OrderAgentRegistration.AgentName, router.ResolveAgentName("order"));
        Assert.Same(agent, registry.GetAgent("OrderAgent"));

        var functionNames = agent.Kernel.Plugins
            .SelectMany(plugin => plugin)
            .Select(function => function.Name)
            .ToArray();

        Assert.Single(agent.Kernel.Plugins);
        Assert.Equal(3, functionNames.Length);
        Assert.Contains("get_order_by_number", functionNames);
        Assert.DoesNotContain("get_all_orders", functionNames);
        Assert.Contains(
            agent.Kernel.AutoFunctionInvocationFilters,
            filter => filter is OrderExactLookupTerminationFilter);
    }

    [Fact]
    public void CustomerAndOrderAgents_UseSeparateKernelsAndPlugins()
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

        var customerAgent = CustomerAgentRegistration.Register(registrar, new RecordingCustomerService());
        var orderAgent = OrderAgentRegistration.Register(registrar, new RecordingOrderService());

        Assert.NotSame(customerAgent.Kernel, orderAgent.Kernel);

        var customerFunctions = customerAgent.Kernel.Plugins
            .SelectMany(plugin => plugin)
            .Select(function => function.Name)
            .ToArray();
        var orderFunctions = orderAgent.Kernel.Plugins
            .SelectMany(plugin => plugin)
            .Select(function => function.Name)
            .ToArray();

        Assert.DoesNotContain("get_order_by_number", customerFunctions);
        Assert.DoesNotContain("search_customers_by_name", orderFunctions);
    }
}

public sealed class OrderLookupResponseFormatterTests
{
    [Fact]
    public void OrderNumberExactLookup_UsesFastPathStatus()
    {
        var order = CreateSampleOrder();

        bool formatted = OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_order_by_number",
            order,
            "AYG-DEMO-1004 siparişinin durumu nedir?",
            out string text);

        Assert.True(formatted);
        Assert.Equal("Siparişin durumu Hazırlanıyor.", text);
    }

    [Fact]
    public void LatestOrderSingleResult_UsesFastPathSummary()
    {
        var order = CreateSampleOrder();

        bool formatted = OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_latest_customer_order",
            order,
            "1 numaralı müşterinin son siparişi nedir?",
            out string text);

        Assert.True(formatted);
        Assert.Contains("Hazırlanıyor", text, StringComparison.Ordinal);
        Assert.DoesNotContain("**", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOrder_ReturnsNotFoundWithoutHallucination()
    {
        bool formatted = OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_order_by_number",
            null,
            "AYG-DEMO-9999 siparişinin durumu ne?",
            out string text);

        Assert.True(formatted);
        Assert.Equal("Sipariş bulunamadı.", text);
    }

    [Fact]
    public void FullInfoRequest_ReturnsFullSummary()
    {
        var order = CreateSampleOrder();

        bool formatted = OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_order_by_number",
            order,
            "AYG-DEMO-1004 siparişinin bilgilerini getir.",
            out string text);

        Assert.True(formatted);
        Assert.Contains("Sipariş Bilgileri", text, StringComparison.Ordinal);
        Assert.Contains("Sipariş No: AYG-DEMO-1004", text, StringComparison.Ordinal);
        Assert.Contains("Durum: Hazırlanıyor", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusSpecificFormatter_ReturnsOnlyStatus()
    {
        var order = CreateSampleOrder();

        bool formatted = OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_order_by_number",
            order,
            "durumu neydi?",
            out string text);

        Assert.True(formatted);
        Assert.Contains("durumu Hazırlanıyor", text, StringComparison.Ordinal);
        Assert.DoesNotContain("**", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Toplam Tutar", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderNumberFollowUp_ReturnsOrderNumberOnly()
    {
        var order = CreateSampleOrder();

        bool formatted = OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_latest_customer_order",
            order,
            "sipariş numarası neydi?",
            out string text);

        Assert.True(formatted);
        Assert.Equal("Sipariş numarası AYG-DEMO-1004.", text);
        Assert.DoesNotContain("**", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerOrdersList_MinimizesDetails()
    {
        IReadOnlyList<OrderAgentResult> orders =
        [
            new(1, "AYG-DEMO-1001", DateTime.UtcNow, "Teslim edildi", 100m),
            new(2, "AYG-DEMO-1002", DateTime.UtcNow, "İptal edildi", 200m)
        ];

        bool formatted = OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_customer_orders",
            orders,
            "1 numaralı müşterinin siparişlerini göster.",
            out string text);

        Assert.True(formatted);
        Assert.Contains("1 numaralı müşterinin siparişleri:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("**", text, StringComparison.Ordinal);
        Assert.Contains("AYG-DEMO-1001", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Toplam Tutar", text, StringComparison.Ordinal);
    }

    private static OrderAgentResult CreateSampleOrder()
    {
        return new OrderAgentResult(4, "AYG-DEMO-1004", new DateTime(2026, 2, 22, 10, 0, 0, DateTimeKind.Utc), "Hazırlanıyor", 910.75m);
    }
}

public sealed class OrderFollowUpResolverTests
{
    [Fact]
    public void SameSessionOrderFollowUp_Works()
    {
        var history = new ChatHistory();
        history.AddUserMessage("AYG-DEMO-1004 siparişinin bilgilerini getir.");
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        Assert.True(OrderFollowUpResolver.IsOrderFollowUp(history, "durumu neydi?"));
    }

    [Fact]
    public void ClearRemovesOrderReference()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");
        Assert.True(OrderFollowUpResolver.HistoryHasOrderReference(history));

        Assert.False(OrderFollowUpResolver.IsOrderFollowUp(new ChatHistory(), "durumu neydi?"));
    }

    [Fact]
    public void SeparateSessions_DoNotLeakOrderContext()
    {
        var sessionA = new ChatHistory();
        sessionA.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        var sessionB = new ChatHistory();
        sessionB.AddAssistantMessage("Sipariş AYG-DEMO-2001 durumu: Bekliyor.");

        Assert.True(OrderFollowUpResolver.IsOrderFollowUp(sessionA, "durumu neydi?"));
        Assert.True(OrderFollowUpResolver.IsOrderFollowUp(sessionB, "durumu neydi?"));
        Assert.Contains("1004", sessionA[^1].Content!, StringComparison.Ordinal);
        Assert.Contains("2001", sessionB[^1].Content!, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerThenOrder_ContextUsesMostRecentOrder()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        Assert.True(OrderFollowUpResolver.IsOrderFollowUp(history, "durumu neydi?"));
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(history, "durumu neydi?"));
    }
}

internal sealed class RecordingOrderService : IOrderService
{
    public OrderDto? OrderByNumber { get; init; }
    public IReadOnlyList<OrderDto> CustomerOrders { get; init; } = [];
    public OrderDto? LatestOrder { get; init; }

    public int GetByNumberCallCount { get; private set; }
    public int GetCustomerOrdersCallCount { get; private set; }
    public int GetLatestCustomerOrderCallCount { get; private set; }
    public string? LastOrderNumber { get; private set; }
    public int LastCustomerId { get; private set; }

    public Task<OrderDto?> GetOrderByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult<OrderDto?>(null);

    public Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        GetByNumberCallCount++;
        LastOrderNumber = orderNumber;
        return Task.FromResult(OrderByNumber);
    }

    public Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(
        int customerId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        GetCustomerOrdersCallCount++;
        LastCustomerId = customerId;
        return Task.FromResult(CustomerOrders);
    }

    public Task<OrderDto?> GetLatestCustomerOrderAsync(int customerId, CancellationToken cancellationToken = default)
    {
        GetLatestCustomerOrderCallCount++;
        LastCustomerId = customerId;
        return Task.FromResult(LatestOrder);
    }
}
