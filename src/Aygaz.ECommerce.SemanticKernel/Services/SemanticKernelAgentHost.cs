#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Execution;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Observability;
using Aygaz.AgentFramework.Routing;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Aygaz.ECommerce.SemanticKernel.Guardrails;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Agents;

namespace Aygaz.ECommerce.SemanticKernel.Services;

public sealed class SemanticKernelAgentHost
{
    public SemanticKernelAgentHost(
        SemanticKernelOptions options,
        IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scopeFactory);

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
        CustomerAgentRegistration.Register(registrar, customerService);

        var orderService = new ScopedOrderServiceAccessor(scopeFactory);
        OrderAgentRegistration.Register(registrar, orderService);
    }

    public SemanticKernelOptions Options { get; }

    public KernelInvocationTelemetry Telemetry { get; }

    public IAgentRegistry Registry { get; }

    public IAgentRouter Router { get; }

    public IAgentRunner Runner { get; }

    public IAygazDomainGuardrail Guardrail { get; }

    public ChatCompletionAgent CustomerAgent =>
        Registry.GetAgent(CustomerAgentRegistration.AgentName);

    public ChatCompletionAgent OrderAgent =>
        Registry.GetAgent(OrderAgentRegistration.AgentName);
}
