using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Guardrails;
using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Web.Configuration;
using Aygaz.ECommerce.Web.Services;

namespace Aygaz.ECommerce.Web;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Services
            .AddOptions<ChatApiOptions>()
            .Bind(builder.Configuration.GetSection(ChatApiOptions.SectionName))
            .Validate(
                options => options.MaxMessageLength is >= 1 and <= 2000,
                "ChatApi:MaxMessageLength 1 ile 2000 arasında olmalıdır.");

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddLocalLlm(builder.Configuration);
        builder.Services.AddLocalRag(builder.Configuration);
        builder.Services.AddCustomerData(builder.Configuration);
        builder.Services.AddDomainGuardrails(builder.Configuration);
        builder.Services.AddCustomerAgent(builder.Configuration);

        builder.Services.AddSingleton<IDomainGuardrailLogger, NullDomainGuardrailLogger>();
        builder.Services.AddSingleton<IChatSessionStore, InMemoryChatSessionStore>();

        WebApplication app = builder.Build();

        await InitializeDatabaseAsync(app.Services);
        await InitializeMongoDatabaseAsync(app.Services);
        await InitializeRagAsync(app.Services, app.Configuration);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapFallbackToFile("index.html");

        string baseUrl = app.Urls.FirstOrDefault() ?? "http://localhost:5280";
        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("Aygaz E-Commerce AI Agent — Web Demo");
        Console.WriteLine("=========================================");
        Console.WriteLine($"Chat UI:   {baseUrl}");
        Console.WriteLine($"Health:    {baseUrl}/health");
        Console.WriteLine($"Swagger:   {baseUrl}/swagger");
        Console.WriteLine("Localhost-only MVP. Authentication yok.");
        Console.WriteLine();

        await app.RunAsync();
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetService<IRelationalDatabaseInitializer>();
        if (initializer is null)
        {
            return;
        }

        await initializer.InitializeAsync();
    }

    private static async Task InitializeMongoDatabaseAsync(IServiceProvider serviceProvider)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetService<IMongoDatabaseInitializer>();
        if (initializer is null)
        {
            return;
        }

        await initializer.InitializeAsync();
    }

    private static async Task InitializeRagAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("Rag:AutoIngestOnStartup", true))
        {
            return;
        }

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IDocumentIngestionService? ingestionService = scope.ServiceProvider.GetService<IDocumentIngestionService>();
        if (ingestionService is null)
        {
            return;
        }

        DocumentIngestionResult result = await ingestionService.IngestAsync();
        Console.WriteLine(
            $"RAG dokuman index hazir: {result.DocumentCount} dokuman, {result.ChunkCount} chunk.");
    }
}
