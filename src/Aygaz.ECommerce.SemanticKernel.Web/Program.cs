using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.SemanticKernel.Configuration;
using Aygaz.ECommerce.SemanticKernel.Web.Configuration;
using Aygaz.ECommerce.SemanticKernel.Web.Services;

namespace Aygaz.ECommerce.SemanticKernel.Web;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddOptions<ChatApiOptions>()
            .Bind(builder.Configuration.GetRequiredSection(ChatApiOptions.SectionName))
            .Validate(
                options => options.MaxMessageLength is >= 1 and <= 4000,
                "ChatApi:MaxMessageLength 1 ile 4000 arasında olmalıdır.");

        builder.Services.AddControllers();
        builder.Services.AddCustomerData(builder.Configuration);
        builder.Services.AddSemanticKernelCustomerStack(builder.Configuration);
        builder.Services.AddSingleton<IChatSessionStore, InMemoryChatSessionStore>();

        WebApplication app = builder.Build();

        await InitializeDatabaseAsync(app.Services);
        await InitializeMongoDatabaseAsync(app.Services);

        if (args.Any(argument => argument.Equals(
            "--seed-mongodb",
            StringComparison.OrdinalIgnoreCase)))
        {
            if (app.Environment.IsProduction()
                && !app.Configuration.GetValue<bool>("DataAccess:MongoDb:AllowProductionSeed"))
            {
                throw new InvalidOperationException(
                    "Production MongoDB seed için DataAccess:MongoDb:AllowProductionSeed=true gereklidir.");
            }

            await SeedMongoDbAsync(app.Services);
            return;
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapFallbackToFile("index.html");

        string baseUrl = app.Urls.FirstOrDefault() ?? "http://localhost:5190";
        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("Aygaz AI Assistant — Semantic Kernel Web");
        Console.WriteLine("=========================================");
        Console.WriteLine($"Chat UI:   {baseUrl}");
        Console.WriteLine($"Health:    {baseUrl}/health");
        Console.WriteLine();

        await app.RunAsync();
    }

    private static async Task SeedMongoDbAsync(IServiceProvider serviceProvider)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        MongoDummyDataSeeder seeder = scope.ServiceProvider.GetRequiredService<MongoDummyDataSeeder>();
        await seeder.SeedAsync();
        Console.WriteLine("MongoDB dummy verileri hazırlandı.");
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IRelationalDatabaseInitializer? initializer = scope.ServiceProvider.GetService<IRelationalDatabaseInitializer>();
        if (initializer is not null)
        {
            await initializer.InitializeAsync();
        }
    }

    private static async Task InitializeMongoDatabaseAsync(IServiceProvider serviceProvider)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IMongoDatabaseInitializer? initializer = scope.ServiceProvider.GetService<IMongoDatabaseInitializer>();
        if (initializer is not null)
        {
            await initializer.InitializeAsync();
        }
    }
}
