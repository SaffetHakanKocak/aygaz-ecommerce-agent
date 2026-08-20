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
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class DomainGuardrailTests
{
    [Fact]
    public async Task Allowed_ContinuesToRoutingAndBusinessAgent()
    {
        var harness = CreateHarness(DomainDecision.Allowed);

        DomainGuardedQueryResult result = await harness.Executor.ExecuteAsync("1 numaralı müşteri kim?");

        Assert.Equal(DomainDecision.Allowed, result.Guardrail.Decision);
        Assert.Equal(1, harness.Router.ResolveCount);
        Assert.Equal(1, harness.Runner.InvokeCount);
        Assert.True(result.BusinessAgentInvoked);
        Assert.Equal("agent-ok", result.Content);
    }

    [Fact]
    public async Task OutOfScope_DoesNotRunBusinessAgent()
    {
        var harness = CreateHarness(DomainDecision.OutOfScope);

        DomainGuardedQueryResult result = await harness.Executor.ExecuteAsync(
            "Arçelik'in satış rakamları nedir?");

        Assert.Equal(DomainDecision.OutOfScope, result.Guardrail.Decision);
        Assert.Equal(0, harness.Router.ResolveCount);
        Assert.Equal(0, harness.Runner.InvokeCount);
        Assert.False(result.BusinessAgentInvoked);
        Assert.False(result.BusinessFunctionInvoked);
    }

    [Fact]
    public async Task Ambiguous_DoesNotRunBusinessAgent()
    {
        var harness = CreateHarness(DomainDecision.Ambiguous);

        DomainGuardedQueryResult result = await harness.Executor.ExecuteAsync("satış rakamları nedir?");

        Assert.Equal(DomainDecision.Ambiguous, result.Guardrail.Decision);
        Assert.Equal(0, harness.Router.ResolveCount);
        Assert.Equal(0, harness.Runner.InvokeCount);
        Assert.False(result.BusinessAgentInvoked);
        Assert.False(result.BusinessFunctionInvoked);
    }

    [Fact]
    public async Task OutOfScope_ReturnsDeterministicResponse()
    {
        var harness = CreateHarness(DomainDecision.OutOfScope);

        DomainGuardedQueryResult result = await harness.Executor.ExecuteAsync(
            "Türkiye'nin başkenti neresi?");

        Assert.Equal(DomainGuardedQueryExecutor.OutOfScopeResponse, result.Content);
    }

    [Fact]
    public async Task Ambiguous_ReturnsDeterministicResponse()
    {
        var harness = CreateHarness(DomainDecision.Ambiguous);

        DomainGuardedQueryResult result = await harness.Executor.ExecuteAsync("satış rakamları nedir?");

        Assert.Equal(DomainGuardedQueryExecutor.AmbiguousResponse, result.Content);
    }

    [Fact]
    public async Task OutOfScope_DoesNotInvokeCustomerKernelFunctions()
    {
        var customers = new RecordingCustomerService();
        var options = new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.Ollama,
            ModelId = "dummy",
            Endpoint = "http://localhost:11434"
        };
        var registry = new AgentRegistry();
        var innerRouter = new AgentRouter();
        var telemetry = new KernelInvocationTelemetry();
        var registrar = new SemanticKernelAgentRegistrar(
            new SemanticKernelFactory(options),
            new SemanticKernelAgentFactory(options),
            registry,
            innerRouter);
        CustomerAgentRegistration.Register(registrar, customers);

        var router = new RecordingRouter(innerRouter);
        var runner = new ThrowingAgentRunner();
        var executor = new DomainGuardedQueryExecutor(
            new StubGuardrail(DomainDecision.OutOfScope),
            router,
            registry,
            runner,
            telemetry);

        DomainGuardedQueryResult result = await executor.ExecuteAsync("Turkcell müşterilerini getir.");

        Assert.Equal(DomainGuardedQueryExecutor.OutOfScopeResponse, result.Content);
        Assert.Equal(0, router.ResolveCount);
        Assert.Equal(0, runner.InvokeCount);
        Assert.Equal(0, telemetry.FunctionInvocationCount);
        Assert.Equal(0, customers.GetByEmailCallCount);
        Assert.Equal(0, customers.GetByIdCallCount);
        Assert.Equal(0, customers.SearchByNameCallCount);
        Assert.Equal(0, customers.GetAllCallCount);
        Assert.False(result.BusinessAgentInvoked);
        Assert.False(result.BusinessFunctionInvoked);
    }

    [Fact]
    public void FrameworkAssembly_HasNoAygazGuardrailLogic()
    {
        var names = typeof(SemanticKernelFactory).Assembly.GetTypes().Select(type => type.Name);

        Assert.DoesNotContain(names, name => name.Contains("Guardrail", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("AygazDomain", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Equals("DomainDecision", StringComparison.Ordinal));
    }

    [Fact]
    public void CustomerAgent_RemainsIsolatedFromGuardrailPlugins()
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

        var agent = CustomerAgentRegistration.Register(registrar, new RecordingCustomerService());
        var functionNames = agent.Kernel.Plugins
            .SelectMany(plugin => plugin)
            .Select(function => function.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            ["get_customer_by_email", "get_customer_by_id", "search_customers_by_name"],
            functionNames);
        Assert.DoesNotContain(
            functionNames,
            name => name.Contains("guardrail", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(typeof(CustomerPlugin).Assembly, typeof(AygazDomainGuardrail).Assembly);
        Assert.NotEqual(typeof(SemanticKernelFactory).Assembly, typeof(AygazDomainGuardrail).Assembly);
    }

    [Theory]
    [InlineData("""{"decision":"Allowed"}""", DomainDecision.Allowed)]
    [InlineData("""{"decision":"OutOfScope"}""", DomainDecision.OutOfScope)]
    [InlineData("""{"decision":"Ambiguous"}""", DomainDecision.Ambiguous)]
    [InlineData("Allowed", DomainDecision.Allowed)]
    public void Parser_ReadsClassifierDecision(string content, DomainDecision expected)
    {
        Assert.True(DomainDecisionParser.TryParse(content, out DomainDecision decision));
        Assert.Equal(expected, decision);
    }

    [Fact]
    public void Parser_UnknownContent_FailsClosed()
    {
        Assert.False(DomainDecisionParser.TryParse("not-a-decision", out _));
    }

    private static Harness CreateHarness(DomainDecision decision)
    {
        var innerRouter = new AgentRouter();
        innerRouter.MapRoute(CustomerAgentRegistration.RouteKey, CustomerAgentRegistration.AgentName);
        var router = new RecordingRouter(innerRouter);
        var registry = new AgentRegistry();
        var kernel = Microsoft.SemanticKernel.Kernel.CreateBuilder().Build();
        registry.Register(new ChatCompletionAgent
        {
            Name = CustomerAgentRegistration.AgentName,
            Kernel = kernel
        });
        var runner = new RecordingAgentRunner();
        var executor = new DomainGuardedQueryExecutor(
            new StubGuardrail(decision),
            router,
            registry,
            runner);

        return new Harness(executor, router, runner);
    }

    private sealed record Harness(
        DomainGuardedQueryExecutor Executor,
        RecordingRouter Router,
        RecordingAgentRunner Runner);

    private sealed class StubGuardrail : IAygazDomainGuardrail
    {
        private readonly DomainDecision _decision;

        public StubGuardrail(DomainDecision decision)
        {
            _decision = decision;
        }

        public Task<AygazDomainGuardrailResult> EvaluateAsync(
            string? userMessage,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AygazDomainGuardrailResult(_decision, TimeSpan.Zero, 0));
        }
    }

    private sealed class RecordingRouter : IAgentRouter
    {
        private readonly IAgentRouter _inner;

        public RecordingRouter(IAgentRouter inner)
        {
            _inner = inner;
        }

        public int ResolveCount { get; private set; }

        public void MapRoute(string routeKey, string agentName) => _inner.MapRoute(routeKey, agentName);

        public string? ResolveAgentName(string routeKey)
        {
            ResolveCount++;
            return _inner.ResolveAgentName(routeKey);
        }

        public IReadOnlyList<string> GetRouteKeys() => _inner.GetRouteKeys();
    }

    private sealed class RecordingAgentRunner : IAgentRunner
    {
        public int InvokeCount { get; private set; }

        public Task<AgentResponse> InvokeAsync(
            ChatCompletionAgent agent,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            InvokeCount++;
            return Task.FromResult(new AgentResponse("agent-ok", TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class ThrowingAgentRunner : IAgentRunner
    {
        public int InvokeCount { get; private set; }

        public Task<AgentResponse> InvokeAsync(
            ChatCompletionAgent agent,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            InvokeCount++;
            throw new InvalidOperationException("Business agent must not run for blocked domain decisions.");
        }
    }
}
