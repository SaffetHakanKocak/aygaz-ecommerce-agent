#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Aygaz.AgentFramework.Agents;

public sealed class SemanticKernelAgentRegistrar : IAgentRegistrar
{
    private readonly IKernelFactory _kernelFactory;
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentRegistry _registry;
    private readonly IAgentRouter _router;

    public SemanticKernelAgentRegistrar(
        IKernelFactory kernelFactory,
        IAgentFactory agentFactory,
        IAgentRegistry registry,
        IAgentRouter router)
    {
        _kernelFactory = kernelFactory;
        _agentFactory = agentFactory;
        _registry = registry;
        _router = router;
    }

    private readonly IReadOnlyList<IFunctionInvocationFilter>? _filters;
    private readonly IReadOnlyList<IAutoFunctionInvocationFilter>? _autoFunctionInvocationFilters;

    public SemanticKernelAgentRegistrar(
        IKernelFactory kernelFactory,
        IAgentFactory agentFactory,
        IAgentRegistry registry,
        IAgentRouter router,
        IReadOnlyList<IFunctionInvocationFilter>? filters,
        IReadOnlyList<IAutoFunctionInvocationFilter>? autoFunctionInvocationFilters = null)
        : this(kernelFactory, agentFactory, registry, router)
    {
        _filters = filters;
        _autoFunctionInvocationFilters = autoFunctionInvocationFilters;
    }

    private readonly object _sync = new();

    public ChatCompletionAgent Register(AgentRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.RouteKey);
        ArgumentNullException.ThrowIfNull(registration.Definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.Definition.Name);

        lock (_sync)
        {
            if (_registry.TryGetAgent(registration.Definition.Name, out _))
            {
                throw new InvalidOperationException(
                    $"Agent '{registration.Definition.Name}' is already registered.");
            }

            if (_router.ResolveAgentName(registration.RouteKey) is not null)
            {
                throw new InvalidOperationException(
                    $"Route '{registration.RouteKey}' is already mapped.");
            }

            Microsoft.SemanticKernel.Kernel kernel = _kernelFactory.CreateKernel();

            if (_filters != null)
            {
                foreach (var filter in _filters)
                    kernel.FunctionInvocationFilters.Add(filter);
            }

            if (_autoFunctionInvocationFilters != null)
            {
                foreach (var filter in _autoFunctionInvocationFilters)
                    kernel.AutoFunctionInvocationFilters.Add(filter);
            }

            foreach (var plugin in registration.Plugins)
            {
                kernel.Plugins.AddFromObject(plugin);
            }

            ChatCompletionAgent agent = _agentFactory.CreateAgent(registration.Definition, kernel);

            _registry.Register(agent);

            try
            {
                _router.MapRoute(registration.RouteKey, registration.Definition.Name);
            }
            catch
            {
                _registry.TryUnregister(registration.Definition.Name);
                throw;
            }

            return agent;
        }
    }
}
