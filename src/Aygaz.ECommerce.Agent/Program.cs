namespace Aygaz.ECommerce.Agent;

using System.Text;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        try
        {
            using IHost host = CreateHost(args);
            var localLlmService = host.Services.GetRequiredService<ILocalLlmService>();

            PrintHeader();
            await RunChatLoopAsync(localLlmService, cancellationTokenSource.Token);
            Console.WriteLine("Uygulama kapatıldı.");

            return 0;
        }
        catch (OptionsValidationException exception)
        {
            Console.Error.WriteLine("Ollama yapılandırması geçersiz.");
            Console.Error.WriteLine($"Teknik detay: {exception.Message}");
            return 1;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine("Uygulama yapılandırma dosyası okunamadı.");
            Console.Error.WriteLine($"Teknik detay: {exception.Message}");
            return 1;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine("Uygulama yapılandırması yüklenemedi.");
            Console.Error.WriteLine($"Teknik detay: {exception.Message}");
            return 1;
        }
    }

    private static IHost CreateHost(string[] args)
    {
        var settings = new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        };

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(settings);
        builder.Services.AddLocalLlm(builder.Configuration);

        return builder.Build();
    }

    private static async Task RunChatLoopAsync(
        ILocalLlmService localLlmService,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Sorunuz:");
            Console.Write("> ");

            string? question;
            try
            {
                question = await Console.In.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (question is null || question.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(question))
            {
                Console.WriteLine("Lütfen bir soru yazın veya çıkmak için 'exit' girin.");
                Console.WriteLine();
                continue;
            }

            try
            {
                string answer = await localLlmService.AskAsync(question.Trim(), cancellationToken);
                Console.WriteLine();
                Console.WriteLine("Yanıt:");
                Console.WriteLine(answer);
            }
            catch (LocalLlmException exception)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(exception.Message);
                Console.Error.WriteLine($"Teknik detay: {exception.TechnicalDetails}");
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Console.WriteLine();
        }
    }

    private static void PrintHeader()
    {
        Console.WriteLine("=========================================");
        Console.WriteLine("Aygaz E-Commerce AI Agent");
        Console.WriteLine("Local LLM Connection Test");
        Console.WriteLine("=========================================");
        Console.WriteLine();
    }
}
