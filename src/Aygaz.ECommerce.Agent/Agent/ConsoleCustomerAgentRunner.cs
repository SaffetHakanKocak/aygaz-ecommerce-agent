using Aygaz.ECommerce.Agent.Services;

namespace Aygaz.ECommerce.Agent.Agent;

public sealed class ConsoleCustomerAgentRunner(IAgentService agentService)
{
    public async Task<AgentConsoleResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        PrintHeader();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Sorunuz ('back': ana menü, 'exit': çıkış):");
            Console.Write("> ");

            string? input;
            try
            {
                input = await Console.In.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return AgentConsoleResult.ExitApplication;
            }

            if (input is null
                || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return AgentConsoleResult.ExitApplication;
            }

            if (input.Trim().Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                return AgentConsoleResult.BackToMainMenu;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Lütfen bir soru yazın, 'back' veya 'exit' girin.");
                Console.WriteLine();
                continue;
            }

            try
            {
                string answer = await agentService.AskAsync(input.Trim(), cancellationToken);
                Console.WriteLine();
                Console.WriteLine("Agent:");
                Console.WriteLine(answer);
            }
            catch (AgentException exception)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(exception.Message);
                Console.Error.WriteLine($"Teknik detay: {exception.TechnicalDetails}");
            }
            catch (LocalLlmException exception)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(exception.Message);
                Console.Error.WriteLine($"Teknik detay: {exception.TechnicalDetails}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return AgentConsoleResult.ExitApplication;
            }

            Console.WriteLine();
        }

        return AgentConsoleResult.ExitApplication;
    }

    private static void PrintHeader()
    {
        Console.WriteLine();
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("Customer AI Agent");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine();
    }
}
