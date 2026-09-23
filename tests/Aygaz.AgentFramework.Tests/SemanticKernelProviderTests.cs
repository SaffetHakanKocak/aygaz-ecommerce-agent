#pragma warning disable SKEXP0070

using System.Reflection;
using Aygaz.AgentFramework.Agents;
using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.AgentFramework.Tests;

[CollectionDefinition("SemanticKernelProvider", DisableParallelization = true)]
public sealed class SemanticKernelProviderCollection;

[Collection("SemanticKernelProvider")]
public sealed class SemanticKernelProviderTests
{
    [Fact]
    public void ParseProvider_DefaultsBlankToOllama()
    {
        Assert.Equal(SemanticKernelProvider.Ollama, SemanticKernelOptions.ParseProvider(null));
        Assert.Equal(SemanticKernelProvider.Ollama, SemanticKernelOptions.ParseProvider("Ollama"));
        Assert.Equal(SemanticKernelProvider.OpenAI, SemanticKernelOptions.ParseProvider("OpenAI"));
        Assert.Equal(SemanticKernelProvider.Groq, SemanticKernelOptions.ParseProvider("Groq"));
        Assert.Throws<InvalidOperationException>(() => SemanticKernelOptions.ParseProvider("Azure"));
    }

    [Fact]
    public void OllamaProvider_RegistersOllamaChatCompletion_WithConfiguredModelId()
    {
        var options = new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.Ollama,
            ModelId = "qwen3:1.7b",
            Endpoint = "http://localhost:11434"
        };

        var factory = new SemanticKernelFactory(options);
        var kernel = factory.CreateKernel();
        var service = kernel.GetRequiredService<IChatCompletionService>();

        Assert.Equal(SemanticKernelProvider.Ollama, factory.Provider);
        Assert.Equal("qwen3:1.7b", factory.ModelId);
        Assert.Equal("qwen3:1.7b", GetModelId(service));
        Assert.DoesNotContain("OpenAI", DescribeService(service), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenAIProvider_RegistersOpenAIChatCompletion_WithConfiguredModelId()
    {
        using var _ = EnvVarScope.Set(SemanticKernelFactory.OpenAiApiKeyVariableName, "sk-test-not-a-real-key");

        var options = new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.OpenAI,
            ModelId = "gpt-4.1"
        };

        var factory = new SemanticKernelFactory(options);
        var kernel = factory.CreateKernel();
        var service = kernel.GetRequiredService<IChatCompletionService>();
        string pipeline = DescribeService(service);

        Assert.Equal(SemanticKernelProvider.OpenAI, factory.Provider);
        Assert.Equal("gpt-4.1", factory.ModelId);
        Assert.Contains("OpenAI", pipeline, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ollama", pipeline, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("gpt-4.1", GetModelId(service));
    }

    [Fact]
    public void OpenAIProvider_WithoutApiKey_ThrowsControlledException()
    {
        using var _ = EnvVarScope.Set(SemanticKernelFactory.OpenAiApiKeyVariableName, null);

        var factory = new SemanticKernelFactory(new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.OpenAI,
            ModelId = "gpt-4.1"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateKernel());
        Assert.Contains(SemanticKernelFactory.OpenAiApiKeyVariableName, ex.Message);
        Assert.DoesNotContain("sk-", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GroqProvider_WithoutApiKey_ThrowsControlledException()
    {
        using var _ = EnvVarScope.Set(SemanticKernelFactory.GroqApiKeyVariableName, null);

        var factory = new SemanticKernelFactory(new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.Groq,
            ModelId = "openai/gpt-oss-120b"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateKernel());
        Assert.Contains(SemanticKernelFactory.GroqApiKeyVariableName, ex.Message);
        Assert.DoesNotContain("gsk_", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GroqProvider_UsesConfiguredModelIdAndDefaultEndpoint()
    {
        using var _ = EnvVarScope.Set(SemanticKernelFactory.GroqApiKeyVariableName, "gsk_test-not-a-real-key");

        var factory = new SemanticKernelFactory(new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.Groq,
            ModelId = "openai/gpt-oss-120b"
        });
        var kernel = factory.CreateKernel();
        var service = kernel.GetRequiredService<IChatCompletionService>();

        Assert.Equal(SemanticKernelProvider.Groq, factory.Provider);
        Assert.Equal("openai/gpt-oss-120b", factory.ModelId);
        Assert.Equal(SemanticKernelFactory.DefaultGroqEndpoint, factory.Endpoint);
        Assert.Equal("openai/gpt-oss-120b", GetModelId(service));
        Assert.Contains("OpenAI", DescribeService(service), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GroqProvider_UsesConfiguredEndpoint()
    {
        using var _ = EnvVarScope.Set(SemanticKernelFactory.GroqApiKeyVariableName, "gsk_test-not-a-real-key");

        var factory = new SemanticKernelFactory(new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.Groq,
            ModelId = "openai/gpt-oss-120b",
            Endpoint = "https://api.groq.com/openai/v1"
        });

        Assert.Equal("https://api.groq.com/openai/v1", factory.Endpoint);
        factory.CreateKernel();
    }

    [Fact]
    public void AgentFactory_UsesOpenAIExecutionSettings_WhenProviderIsGroq()
    {
        var options = new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.Groq,
            ModelId = "openai/gpt-oss-120b"
        };

        using var _ = EnvVarScope.Set(SemanticKernelFactory.GroqApiKeyVariableName, "gsk_test-not-a-real-key");
        var kernel = new SemanticKernelFactory(options).CreateKernel();
        var agent = new SemanticKernelAgentFactory(options).CreateAgent(
            new AgentDefinition { Name = "Test", Instructions = "test" },
            kernel);

        Assert.NotNull(agent.Arguments?.ExecutionSettings);
        Assert.Contains(
            "OpenAI",
            agent.Arguments.ExecutionSettings.Values.First().GetType().Name,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, ((Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings)
            agent.Arguments.ExecutionSettings.Values.First()).Temperature);
        Assert.Null(((Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings)
            agent.Arguments.ExecutionSettings.Values.First()).MaxTokens);
        Assert.Null(((Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings)
            agent.Arguments.ExecutionSettings.Values.First()).ReasoningEffort);
    }

    [Fact]
    public void FrameworkAssembly_HasNoCustomerDomainTypes()
    {
        var names = typeof(SemanticKernelFactory).Assembly.GetTypes().Select(type => type.Name);

        Assert.DoesNotContain(names, name => name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Order", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Guardrail", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("AygazDomain", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("OutOfScope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AgentFactory_UsesOpenAIExecutionSettings_WhenProviderIsOpenAI()
    {
        var options = new SemanticKernelOptions
        {
            Provider = SemanticKernelProvider.OpenAI,
            ModelId = "gpt-4.1"
        };

        using var _ = EnvVarScope.Set(SemanticKernelFactory.OpenAiApiKeyVariableName, "sk-test-not-a-real-key");
        var kernel = new SemanticKernelFactory(options).CreateKernel();
        var agent = new SemanticKernelAgentFactory(options).CreateAgent(
            new AgentDefinition { Name = "Test", Instructions = "test" },
            kernel);

        Assert.NotNull(agent.Arguments?.ExecutionSettings);
        Assert.Contains(
            "OpenAI",
            agent.Arguments.ExecutionSettings.Values.First().GetType().Name,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetModelId(IChatCompletionService service)
    {
        return service.Attributes.TryGetValue("ModelId", out object? modelId)
            ? modelId?.ToString()
            : null;
    }

    private static string DescribeService(IChatCompletionService service)
    {
        var parts = new List<string> { service.GetType().FullName ?? service.GetType().Name };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in service.GetType().GetFields(flags))
        {
            object? value = field.GetValue(service);
            if (value is not null)
            {
                parts.Add(value.GetType().FullName ?? value.GetType().Name);
            }
        }

        return string.Join(" | ", parts);
    }
}

internal sealed class EnvVarScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previous;

    private EnvVarScope(string name, string? previous)
    {
        _name = name;
        _previous = previous;
    }

    public static EnvVarScope Set(string name, string? value)
    {
        string? previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        return new EnvVarScope(name, previous);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, _previous);
    }
}
