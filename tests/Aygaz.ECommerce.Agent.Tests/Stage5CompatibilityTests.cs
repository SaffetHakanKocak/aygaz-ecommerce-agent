using System.Text.Json;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Tools;

namespace Aygaz.ECommerce.Agent.Tests;

[CollectionDefinition("Console output", DisableParallelization = true)]
public sealed class ConsoleOutputCollectionDefinition
{
}

[Collection("Console output")]
public sealed class Stage5CompatibilityTests
{
    [Fact]
    public void LogArguments_NameQuery_RedactsPersonalData()
    {
        TextWriter originalOutput = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);

            var logger = new ConsoleToolCallLogger();
            logger.LogArguments("query", "Gerçek Kişi Adı");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        Assert.Contains("[Args] query=<redacted>", output.ToString());
        Assert.DoesNotContain("Gerçek Kişi Adı", output.ToString());
    }

    [Theory]
    [InlineData("AYG-DEMO-PRD-001", "AYG-DEMO-PRD-001")]
    [InlineData("REAL-SKU-123", "<redacted>")]
    [InlineData("AYG-DEMO-PRD-ABC", "<redacted>")]
    public void LogArguments_Sku_OnlyExposesStrictDemoFormat(
        string sku,
        string expectedValue)
    {
        TextWriter originalOutput = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);

            var logger = new ConsoleToolCallLogger();
            logger.LogArguments("sku", sku);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        Assert.Contains($"[Args] sku={expectedValue}", output.ToString());
        if (expectedValue == "<redacted>")
        {
            Assert.DoesNotContain(sku, output.ToString());
        }
    }

    [Fact]
    public void OllamaChatSettings_PositionalContract_RemainsBackwardCompatible()
    {
        OllamaToolDefinition[] tools = [];
        JsonElement format = JsonSerializer.SerializeToElement(
            new { type = "object" });

        var settings = new OllamaChatSettings(
            tools,
            format,
            0,
            32,
            true);

        Assert.Same(tools, settings.Tools);
        Assert.True(settings.Format.HasValue);
        Assert.Equal(format, settings.Format.Value);
        Assert.Equal(0d, settings.Temperature);
        Assert.Equal(32, settings.MaxOutputTokens);
        Assert.True(settings.Think is true);
    }
}
