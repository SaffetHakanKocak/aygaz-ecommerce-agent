#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Aygaz.ECommerce.SemanticKernel.Capabilities;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class MultiAgentOperationTests
{
    [Fact]
    public void Register_AllOperationAgents_MapSeparateRoutes()
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
            new SemanticKernelAgentFactory(options),
            registry,
            router);

        CustomerAgentRegistration.Register(registrar, new RecordingCustomerService());
        OrderAgentRegistration.Register(registrar, new RecordingOrderService(), new RecordingOrderOperationService());
        ProductAgentRegistration.Register(registrar, new RecordingProductService());
        InventoryAgentRegistration.Register(
            registrar,
            new RecordingProductService(),
            new RecordingInventoryService());
        SalesAnalyticsAgentRegistration.Register(registrar, new RecordingSalesAnalyticsService(), new CommerceOptions());
        SupportPolicyAgentRegistration.Register(registrar, new RecordingDocumentRetrievalService());

        Assert.Equal(CustomerAgentRegistration.AgentName, router.ResolveAgentName(CustomerAgentRegistration.RouteKey));
        Assert.Equal(OrderAgentRegistration.AgentName, router.ResolveAgentName(OrderAgentRegistration.RouteKey));
        Assert.Equal(ProductAgentRegistration.AgentName, router.ResolveAgentName(ProductAgentRegistration.RouteKey));
        Assert.Equal(InventoryAgentRegistration.AgentName, router.ResolveAgentName(InventoryAgentRegistration.RouteKey));
        Assert.Equal(SalesAnalyticsAgentRegistration.AgentName, router.ResolveAgentName(SalesAnalyticsAgentRegistration.RouteKey));
        Assert.Equal(SupportPolicyAgentRegistration.AgentName, router.ResolveAgentName(SupportPolicyAgentRegistration.RouteKey));

        Assert.Equal(6, router.GetRouteKeys().Count);
        Assert.NotSame(
            registry.GetAgent(CustomerAgentRegistration.AgentName).Kernel,
            registry.GetAgent(OrderAgentRegistration.AgentName).Kernel);
        Assert.NotSame(
            registry.GetAgent(ProductAgentRegistration.AgentName).Kernel,
            registry.GetAgent(InventoryAgentRegistration.AgentName).Kernel);

        string[] inventoryFunctions = registry.GetAgent(InventoryAgentRegistration.AgentName)
            .Kernel
            .Plugins
            .SelectMany(plugin => plugin)
            .Select(function => function.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            ["get_product_by_sku", "get_product_inventory", "get_total_product_stock", "search_products"],
            inventoryFunctions);
    }

    [Theory]
    [InlineData(AygazCapability.Customer, "Ahmet Yilmaz'in telefonu nedir?", "customer")]
    [InlineData(AygazCapability.Order, "AYG-DEMO-1004 siparis durumu nedir?", "order")]
    [InlineData(AygazCapability.ProductInventory, "AYG-DEMO-PRD-001 urununu getir", "product")]
    [InlineData(AygazCapability.ProductInventory, "AYG-DEMO-PRD-001 stokta mi?", "inventory")]
    [InlineData(AygazCapability.Sales, "Aygaz cirosu nedir?", "sales")]
    [InlineData(AygazCapability.Policy, "Aygaz iade politikasi nedir?", "policy")]
    public void RouteResolver_MapsCapabilityToOperationAgent(
        AygazCapability capability,
        string message,
        string expectedRoute)
    {
        Assert.Equal(expectedRoute, MultiAgentRouteResolver.ResolveRouteKey(capability, message));
    }

    private sealed class RecordingProductService : IProductService
    {
        public Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductDto?>(null);

        public Task<ProductDto?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductDto?>(null);

        public Task<IReadOnlyList<ProductDto>> SearchProductsAsync(
            string query,
            int maxResults,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductDto>>([]);
    }

    private sealed class RecordingInventoryService : IInventoryService
    {
        public Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(
            int productId,
            int maxResults,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryDto>>([]);

        public Task<long?> GetTotalAvailableStockAsync(int productId, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);
    }

    private sealed class RecordingSalesAnalyticsService : ISalesAnalyticsService
    {
        public Task<SalesSummaryDto> GetSalesSummaryAsync(
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SalesSummaryDto(0, 0, 0, 0));

        public Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(
            DateOnly fromDate,
            DateOnly toDate,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopSellingProductDto>>([]);

        public Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(
            int customerId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CustomerPurchaseSummaryDto?>(null);
    }

    private sealed class RecordingDocumentRetrievalService : IDocumentRetrievalService
    {
        public int DocumentCount => 0;

        public int ChunkCount => 0;

        public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentSearchResult>>([]);
    }
}
