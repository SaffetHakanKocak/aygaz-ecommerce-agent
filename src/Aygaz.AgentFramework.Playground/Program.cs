#pragma warning disable SKEXP0070

using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Kernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace Aygaz.AgentFramework.Playground;

internal static class Program
{
    private static async Task Main()
    {
        Console.WriteLine("Aygaz Agent Framework - Semantic Kernel Playground");
        Console.WriteLine();

        var options = new SemanticKernelOptions
        {
            ModelId = "qwen3:1.7b",
            Endpoint = "http://localhost:11434"
        };

        var factory = new SemanticKernelFactory(options);
        Microsoft.SemanticKernel.Kernel kernel = factory.CreateKernel();

        kernel.Plugins.AddFromObject(new DemoPlugin());
        Console.WriteLine("Plugin registered: DemoPlugin");
        Console.WriteLine();

        kernel.FunctionInvocationFilters.Add(new FunctionInvocationLogger());

        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new OllamaPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var chatHistory = new ChatHistory(
            "Sen yardımcı bir asistansın. Kullanıcının sorularını yanıtlamak için mevcut fonksiyonları kullan.");

        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)
                || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            chatHistory.AddUserMessage(input);

            try
            {
                ChatMessageContent response = await chatService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings,
                    kernel);

                string answer = response.Content ?? "(boş yanıt)";
                chatHistory.AddAssistantMessage(answer);

                Console.WriteLine();
                Console.WriteLine(answer);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Hata: {ex.Message}");
                Console.WriteLine();
            }
        }
    }
}
