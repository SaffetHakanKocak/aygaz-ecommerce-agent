#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Observability;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Aygaz.ECommerce.SemanticKernel.Guardrails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Agents;

namespace Aygaz.ECommerce.SemanticKernel.Services;

public sealed class SemanticKernelAgentHost
{
    public SemanticKernelAgentHost(
        SemanticKernelOptions options,
        IServiceScopeFactory scopeFactory,
        IOptions<AgentOptions> agentOptions,
        IOptions<CommerceOptions> commerceOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(agentOptions);
        ArgumentNullException.ThrowIfNull(commerceOptions);

        Options = options;
        Telemetry = new KernelInvocationTelemetry();
        var telemetryFilter = new KernelInvocationTelemetryFilter(Telemetry);
        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory(options);
        Registry = new AgentRegistry();
        Router = new AgentRouter();
        var registrar = new SemanticKernelAgentRegistrar(
            kernelFactory,
            agentFactory,
            Registry,
            Router,
            [telemetryFilter],
            [telemetryFilter]);
        Runner = new SemanticKernelAgentRunner();
        Guardrail = new AygazDomainGuardrail(kernelFactory.CreateKernel(), options);

        var customerService = new ScopedCustomerServiceAccessor(scopeFactory);
        CustomerAgentRegistration.Register(registrar, customerService, agentOptions.Value.MaxNameSearchResults);

        var orderService = new ScopedOrderServiceAccessor(scopeFactory);
        OrderAgentRegistration.Register(registrar, orderService, agentOptions.Value.MaxOrderSearchResults);

        ProductService = new ScopedProductServiceAccessor(scopeFactory);
        ProductAgentRegistration.Register(registrar, ProductService, agentOptions.Value.MaxProductSearchResults);

        InventoryService = new ScopedInventoryServiceAccessor(scopeFactory);
        InventoryAgentRegistration.Register(
            registrar,
            ProductService,
            InventoryService,
            agentOptions.Value.MaxProductSearchResults,
            agentOptions.Value.MaxInventoryLocationResults);

        SalesAnalyticsService = new ScopedSalesAnalyticsServiceAccessor(scopeFactory);
        SalesAnalyticsAgentRegistration.Register(registrar, SalesAnalyticsService, commerceOptions.Value);

        DocumentRetrievalService = new ScopedDocumentRetrievalServiceAccessor(scopeFactory);
        SupportPolicyAgentRegistration.Register(registrar, DocumentRetrievalService);
    }

    public SemanticKernelOptions Options { get; }

    public KernelInvocationTelemetry Telemetry { get; }

    public IAgentRegistry Registry { get; }

    public IAgentRouter Router { get; }

    public IAgentRunner Runner { get; }

    public IAygazDomainGuardrail Guardrail { get; }

    public IProductService ProductService { get; }

    public IInventoryService InventoryService { get; }

    public ISalesAnalyticsService SalesAnalyticsService { get; }

    public IDocumentRetrievalService DocumentRetrievalService { get; }

    public ChatCompletionAgent CustomerAgent =>
        Registry.GetAgent(CustomerAgentRegistration.AgentName);

    public ChatCompletionAgent OrderAgent =>
        Registry.GetAgent(OrderAgentRegistration.AgentName);

    public ChatCompletionAgent ProductAgent =>
        Registry.GetAgent(ProductAgentRegistration.AgentName);

    public ChatCompletionAgent InventoryAgent =>
        Registry.GetAgent(InventoryAgentRegistration.AgentName);

    public ChatCompletionAgent SalesAnalyticsAgent =>
        Registry.GetAgent(SalesAnalyticsAgentRegistration.AgentName);

    public ChatCompletionAgent SupportPolicyAgent =>
        Registry.GetAgent(SupportPolicyAgentRegistration.AgentName);
}
