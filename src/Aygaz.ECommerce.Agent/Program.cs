using System.Text;
using Aygaz.ECommerce.Agent.Agent;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent;

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

            if (args.Any(argument => argument.Equals(
                "--seed-mongodb",
                StringComparison.OrdinalIgnoreCase)))
            {
                string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                    ?? "Production";
                if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
                    && !host.Services.GetRequiredService<MongoDbOptions>().AllowProductionSeed)
                {
                    throw new InvalidOperationException(
                        "Production MongoDB seed için DataAccess:MongoDb:AllowProductionSeed=true gereklidir.");
                }

                await SeedMongoDbAsync(host.Services, cancellationTokenSource.Token);
                return 0;
            }

            await InitializeDatabaseAsync(host.Services, cancellationTokenSource.Token);
            await InitializeMongoDatabaseAsync(host.Services, cancellationTokenSource.Token);

            await RunMainMenuAsync(host.Services, cancellationTokenSource.Token);

            Console.WriteLine("Uygulama kapatıldı.");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            Console.WriteLine("Uygulama kapatıldı.");
            return 0;
        }
        catch (DatabaseInitializationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine($"Teknik detay: {exception.TechnicalDetails}");
            return 1;
        }
        catch (OptionsValidationException exception)
        {
            bool isAgentConfiguration = exception.Failures.Any(
                failure => failure.StartsWith("Agent:", StringComparison.Ordinal));
            bool isGuardrailConfiguration = exception.Failures.Any(
                failure => failure.StartsWith("DomainGuardrail:", StringComparison.Ordinal));

            Console.Error.WriteLine(isGuardrailConfiguration
                ? "Domain guardrail yapılandırması geçersiz."
                : isAgentConfiguration
                    ? "Agent yapılandırması geçersiz."
                    : "Ollama yapılandırması geçersiz.");
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
        builder.Logging.ClearProviders();
        builder.Services.AddLocalLlm(builder.Configuration);
        builder.Services.AddLocalRag(builder.Configuration);
        builder.Services.AddCustomerData(builder.Configuration);
        builder.Services.AddDomainGuardrails(builder.Configuration);
        builder.Services.AddCustomerAgent(builder.Configuration);

        return builder.Build();
    }

    private static async Task InitializeDatabaseAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetService<IRelationalDatabaseInitializer>();
        if (initializer is null)
        {
            return;
        }

        await initializer.InitializeAsync(cancellationToken);
    }

    private static async Task InitializeMongoDatabaseAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetService<IMongoDatabaseInitializer>();
        if (initializer is null)
        {
            return;
        }

        await initializer.InitializeAsync(cancellationToken);
    }

    private static async Task SeedMongoDbAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<MongoDummyDataSeeder>();
        await seeder.SeedAsync(cancellationToken);
        Console.WriteLine("MongoDB dummy verileri hazırlandı.");
    }

    private static async Task RunMainMenuAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PrintMainMenu();

            string? selection;
            try
            {
                selection = await Console.In.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            switch (selection?.Trim())
            {
                case "1":
                {
                    var localLlmService =
                        serviceProvider.GetRequiredService<ILocalLlmService>();
                    PrintLocalLlmHeader();
                    await RunChatLoopAsync(localLlmService, cancellationToken);
                    break;
                }
                case "2":
                {
                    await using (AsyncServiceScope scope = serviceProvider.CreateAsyncScope())
                    {
                        var runner = scope.ServiceProvider
                            .GetRequiredService<ConsoleCustomerTestRunner>();
                        await runner.RunAsync(cancellationToken);
                    }

                    break;
                }
                case "3":
                {
                    await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
                    var runner = scope.ServiceProvider
                        .GetRequiredService<ConsoleCustomerAgentRunner>();
                    AgentConsoleResult result = await runner.RunAsync(cancellationToken);

                    if (result == AgentConsoleResult.ExitApplication)
                    {
                        return;
                    }

                    break;
                }
                case "0":
                case null:
                    return;
                default:
                    Console.WriteLine("Geçersiz seçim. Lütfen 0, 1, 2 veya 3 girin.");
                    Console.WriteLine();
                    break;
            }
        }
    }

    private static async Task RunChatLoopAsync(
        ILocalLlmService localLlmService,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Sorunuz (ana menü için 'exit'):");
            Console.Write("> ");

            string? question;
            try
            {
                question = await Console.In.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (question is null || question.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(question))
            {
                Console.WriteLine("Lütfen bir soru yazın veya ana menü için 'exit' girin.");
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Console.WriteLine();
        }
    }

    private static void PrintMainMenu()
    {
        Console.WriteLine("=========================================");
        Console.WriteLine("Aygaz E-Commerce AI Agent");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        Console.WriteLine("1 - Local LLM Test");
        Console.WriteLine("2 - Customer Database Test");
        Console.WriteLine("3 - E-Commerce AI Agent");
        Console.WriteLine("0 - Exit");
        Console.WriteLine();
        Console.WriteLine("Seçiminiz:");
        Console.Write("> ");
    }

    private static void PrintLocalLlmHeader()
    {
        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("Aygaz E-Commerce AI Agent");
        Console.WriteLine("Local LLM Connection Test");
        Console.WriteLine("=========================================");
        Console.WriteLine();
    }
}
