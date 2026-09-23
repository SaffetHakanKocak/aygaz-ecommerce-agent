#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Aygaz.AgentFramework.Routing;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.AgentFramework.Tests;

public class AgentRegistryTests
{
    private static ChatCompletionAgent CreateDummyAgent(string name)
    {
        var options = new SemanticKernelOptions
        {
            ModelId = "dummy",
            Endpoint = "http://localhost:11434"
        };
        var factory = new SemanticKernelFactory(options);
        var kernel = factory.CreateKernel();
        var agentFactory = new SemanticKernelAgentFactory();
        return agentFactory.CreateAgent(new AgentDefinition
        {
            Name = name,
            Instructions = "test"
        }, kernel);
    }

    [Fact]
    public void Register_And_GetAgent_Works()
    {
        var registry = new AgentRegistry();
        var agent = CreateDummyAgent("TestAgent");

        registry.Register(agent);

        var found = registry.GetAgent("TestAgent");
        Assert.Same(agent, found);
    }

    [Fact]
    public void GetAgent_CaseInsensitive()
    {
        var registry = new AgentRegistry();
        registry.Register(CreateDummyAgent("TestAgent"));

        var found = registry.GetAgent("testagent");
        Assert.Equal("TestAgent", found.Name);
    }

    [Fact]
    public void GetAgent_NotFound_Throws()
    {
        var registry = new AgentRegistry();

        Assert.Throws<KeyNotFoundException>(() => registry.GetAgent("NonExistent"));
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var registry = new AgentRegistry();
        registry.Register(CreateDummyAgent("TestAgent"));

        Assert.Throws<InvalidOperationException>(() => registry.Register(CreateDummyAgent("TestAgent")));
    }

    [Fact]
    public void GetAgentNames_ReturnsAll()
    {
        var registry = new AgentRegistry();
        registry.Register(CreateDummyAgent("A"));
        registry.Register(CreateDummyAgent("B"));

        var names = registry.GetAgentNames();
        Assert.Contains("A", names);
        Assert.Contains("B", names);
        Assert.Equal(2, names.Count);
    }
}

public class AgentRouterTests
{
    [Fact]
    public void MapRoute_And_Resolve()
    {
        var router = new AgentRouter();
        router.MapRoute("system", "SystemAgent");

        Assert.Equal("SystemAgent", router.ResolveAgentName("system"));
    }

    [Fact]
    public void Resolve_Unknown_ReturnsNull()
    {
        var router = new AgentRouter();

        Assert.Null(router.ResolveAgentName("unknown"));
    }

    [Fact]
    public void GetRouteKeys_ReturnsAll()
    {
        var router = new AgentRouter();
        router.MapRoute("a", "AgentA");
        router.MapRoute("b", "AgentB");

        var keys = router.GetRouteKeys();
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }
}

public class AgentIsolationTests
{
    [Fact]
    public void Agents_Have_Separate_Kernels()
    {
        var options = new SemanticKernelOptions
        {
            ModelId = "dummy",
            Endpoint = "http://localhost:11434"
        };
        var factory = new SemanticKernelFactory(options);

        var kernel1 = factory.CreateKernel();
        var kernel2 = factory.CreateKernel();

        Assert.NotSame(kernel1, kernel2);
    }
}

public class AgentRegistrarTests
{
    private static SemanticKernelAgentRegistrar CreateRegistrar(
        out AgentRegistry registry,
        out AgentRouter router)
    {
        var options = new SemanticKernelOptions
        {
            ModelId = "dummy",
            Endpoint = "http://localhost:11434"
        };
        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory();
        registry = new AgentRegistry();
        router = new AgentRouter();
        return new SemanticKernelAgentRegistrar(kernelFactory, agentFactory, registry, router);
    }

    private static AgentRegistration CreateRegistration(string name, string route)
    {
        return new AgentRegistration
        {
            RouteKey = route,
            Definition = new AgentDefinition { Name = name, Instructions = "test" },
            Plugins = [new DummyPlugin()]
        };
    }

    [Fact]
    public void Register_CreatesAgent_And_AddsToRegistry()
    {
        var registrar = CreateRegistrar(out var registry, out _);

        var agent = registrar.Register(CreateRegistration("TestAgent", "test"));

        Assert.Equal("TestAgent", agent.Name);
        Assert.Same(agent, registry.GetAgent("TestAgent"));
    }

    [Fact]
    public void Register_MapsRoute()
    {
        var registrar = CreateRegistrar(out _, out var router);

        registrar.Register(CreateRegistration("TestAgent", "test"));

        Assert.Equal("TestAgent", router.ResolveAgentName("test"));
    }

    [Fact]
    public void Register_Creates_Separate_Kernels()
    {
        var registrar = CreateRegistrar(out var registry, out _);

        registrar.Register(CreateRegistration("A", "a"));
        registrar.Register(CreateRegistration("B", "b"));

        var agentA = registry.GetAgent("A");
        var agentB = registry.GetAgent("B");
        Assert.NotSame(agentA.Kernel, agentB.Kernel);
    }

    [Fact]
    public void Register_PluginIsolation()
    {
        var registrar = CreateRegistrar(out var registry, out _);

        registrar.Register(new AgentRegistration
        {
            RouteKey = "x",
            Definition = new AgentDefinition { Name = "X", Instructions = "t" },
            Plugins = [new DummyPlugin()]
        });
        registrar.Register(new AgentRegistration
        {
            RouteKey = "y",
            Definition = new AgentDefinition { Name = "Y", Instructions = "t" },
            Plugins = [new DummyPlugin2()]
        });

        var agentX = registry.GetAgent("X");
        var agentY = registry.GetAgent("Y");

        var xFunctions = agentX.Kernel.Plugins.SelectMany(p => p).Select(f => f.Name).ToList();
        var yFunctions = agentY.Kernel.Plugins.SelectMany(p => p).Select(f => f.Name).ToList();

        Assert.Contains("dummy_func", xFunctions);
        Assert.DoesNotContain("dummy_func2", xFunctions);
        Assert.Contains("dummy_func2", yFunctions);
        Assert.DoesNotContain("dummy_func", yFunctions);
    }

    [Fact]
    public void Register_DuplicateName_Throws()
    {
        var registrar = CreateRegistrar(out _, out _);
        registrar.Register(CreateRegistration("Dup", "route1"));

        Assert.Throws<InvalidOperationException>(() =>
            registrar.Register(CreateRegistration("Dup", "route2")));
    }

    [Fact]
    public void SuccessfulRegistration_CreatesAgentAndRoute()
    {
        var registrar = CreateRegistrar(out var registry, out var router);

        var agent = registrar.Register(CreateRegistration("SystemAgent", "system"));

        Assert.Equal("SystemAgent", agent.Name);
        Assert.Same(agent, registry.GetAgent("SystemAgent"));
        Assert.Equal("SystemAgent", router.ResolveAgentName("system"));
    }

    [Fact]
    public void DuplicateAgentName_DoesNotCreateRoute()
    {
        var registrar = CreateRegistrar(out var registry, out var router);
        registrar.Register(CreateRegistration("Dup", "route1"));

        Assert.Throws<InvalidOperationException>(() =>
            registrar.Register(CreateRegistration("Dup", "route2")));

        Assert.Same(registry.GetAgent("Dup"), registry.GetAgent("Dup"));
        Assert.Equal("Dup", router.ResolveAgentName("route1"));
        Assert.Null(router.ResolveAgentName("route2"));
        Assert.Single(registry.GetAgentNames());
    }

    [Fact]
    public void DuplicateRoute_DoesNotLeaveAgentInRegistry()
    {
        var registrar = CreateRegistrar(out var registry, out var router);
        registrar.Register(CreateRegistration("SystemAgent", "system"));

        Assert.Throws<InvalidOperationException>(() =>
            registrar.Register(CreateRegistration("OtherAgent", "system")));

        Assert.False(registry.TryGetAgent("OtherAgent", out _));
        Assert.Single(registry.GetAgentNames());
        Assert.Equal("SystemAgent", router.ResolveAgentName("system"));
    }

    [Fact]
    public void DuplicateRoute_DoesNotChangeExistingRoute()
    {
        var registrar = CreateRegistrar(out var registry, out var router);
        var original = registrar.Register(CreateRegistration("SystemAgent", "system"));

        Assert.Throws<InvalidOperationException>(() =>
            registrar.Register(CreateRegistration("OtherAgent", "system")));

        Assert.Same(original, registry.GetAgent("SystemAgent"));
        Assert.Equal("SystemAgent", router.ResolveAgentName("system"));
        Assert.False(registry.TryGetAgent("OtherAgent", out _));
    }

    [Fact]
    public void FailedRegistration_DoesNotLeavePartialState()
    {
        var options = new SemanticKernelOptions
        {
            ModelId = "dummy",
            Endpoint = "http://localhost:11434"
        };
        var kernelFactory = new SemanticKernelFactory(options);
        var agentFactory = new SemanticKernelAgentFactory();
        var registry = new AgentRegistry();
        var router = new ThrowingAfterFirstMapRouter();
        var registrar = new SemanticKernelAgentRegistrar(kernelFactory, agentFactory, registry, router);

        registrar.Register(CreateRegistration("FirstAgent", "first"));

        Assert.Throws<InvalidOperationException>(() =>
            registrar.Register(CreateRegistration("SecondAgent", "second")));

        Assert.False(registry.TryGetAgent("SecondAgent", out _));
        Assert.Null(router.ResolveAgentName("second"));
        Assert.Equal("FirstAgent", router.ResolveAgentName("first"));
        Assert.True(registry.TryGetAgent("FirstAgent", out _));
        Assert.Single(registry.GetAgentNames());
    }
}

public sealed class DummyPlugin
{
    [Microsoft.SemanticKernel.KernelFunction("dummy_func")]
    public string Dummy() => "x";
}

public sealed class DummyPlugin2
{
    [Microsoft.SemanticKernel.KernelFunction("dummy_func2")]
    public string Dummy2() => "y";
}

internal sealed class ThrowingAfterFirstMapRouter : IAgentRouter
{
    private readonly AgentRouter _inner = new();
    private int _mapCount;

    public void MapRoute(string routeKey, string agentName)
    {
        if (Interlocked.Increment(ref _mapCount) > 1)
            throw new InvalidOperationException("MapRoute failed.");

        _inner.MapRoute(routeKey, agentName);
    }

    public string? ResolveAgentName(string routeKey) => _inner.ResolveAgentName(routeKey);

    public IReadOnlyList<string> GetRouteKeys() => _inner.GetRouteKeys();
}
