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
