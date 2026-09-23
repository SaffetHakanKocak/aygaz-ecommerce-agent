using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.Configuration;

public static class CustomerDataServiceCollectionExtensions
{
    private const string ConnectionStringName = "ECommerceDatabase";

    public static IServiceCollection AddCustomerData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        DataAccessOptions dataAccessOptions = configuration
            .GetSection(DataAccessOptions.SectionName)
            .Get<DataAccessOptions>() ?? new DataAccessOptions();

        switch (dataAccessOptions.Provider.Trim().ToLowerInvariant())
        {
            case "mongodb":
            case "mongo":
                RegisterMongoDataAccess(services, dataAccessOptions.MongoDb);
                break;
            case "sqlite":
                RegisterRelationalDataAccess(services, dataAccessOptions.Sqlite.ConnectionString, configuration.GetConnectionString(ConnectionStringName), RelationalDatabaseProvider.Sqlite);
                break;
            case "sqlserver":
            case "mssql":
                RegisterRelationalDataAccess(services, dataAccessOptions.SqlServer.ConnectionString, null, RelationalDatabaseProvider.SqlServer);
                break;
            default:
                throw new InvalidOperationException(
                    "DataAccess:Provider yalnızca MongoDb, Sqlite veya SqlServer olabilir.");
        }

        services.AddSingleton(dataAccessOptions);
        services.AddSingleton(dataAccessOptions.MongoDb);

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderOperationService, OrderOperationService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISalesAnalyticsService, SalesAnalyticsService>();
        services.AddScoped<ConsoleCustomerTestRunner>();

        return services;
    }

    private static void RegisterMongoDataAccess(
        IServiceCollection services,
        MongoDbOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("DataAccess:MongoDb:ConnectionString boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            throw new InvalidOperationException("DataAccess:MongoDb:DatabaseName boş olamaz.");
        }

        services.AddSingleton<IMongoClient>(_ => new MongoClient(options.ConnectionString));
        services.AddSingleton<IMongoDatabase>(serviceProvider =>
            serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName));
        services.AddScoped<MongoDataAccess>();
        services.AddScoped<IECommerceDataAccess>(serviceProvider =>
            serviceProvider.GetRequiredService<MongoDataAccess>());
        services.AddScoped<IMongoDatabaseInitializer, MongoDatabaseInitializer>();
        services.AddScoped<MongoDummyDataSeeder>();
    }

    private static void RegisterRelationalDataAccess(
        IServiceCollection services,
        string configuredConnectionString,
        string? fallbackConnectionString,
        RelationalDatabaseProvider provider)
    {
        string connectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? fallbackConnectionString ?? string.Empty
            : configuredConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} boş olamaz.");
        }

        if (provider == RelationalDatabaseProvider.Sqlite)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                throw new InvalidOperationException("SQLite bağlantı dizesi Data Source içermelidir.");
            }

            if (!builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
                && !Path.IsPathFullyQualified(builder.DataSource))
            {
                builder.DataSource = Path.GetFullPath(builder.DataSource, Directory.GetCurrentDirectory());
            }

            connectionString = builder.ConnectionString;
        }

        services.AddScoped<IECommerceDataAccess>(_ => new DapperSqlDataAccess(
            () => provider == RelationalDatabaseProvider.Sqlite
                ? new SqliteConnection(connectionString)
                : new SqlConnection(connectionString),
            provider));
        services.AddScoped<IRelationalDatabaseInitializer>(_ => new DapperRelationalDatabaseInitializer(
            () => provider == RelationalDatabaseProvider.Sqlite
                ? new SqliteConnection(connectionString)
                : new SqlConnection(connectionString),
            provider));
    }

    private static string ResolveConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ConnectionStringName} geçerli bir Data Source içermelidir.");
            }

            if (!builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
                && !Path.IsPathFullyQualified(builder.DataSource))
            {
                builder.DataSource = Path.GetFullPath(
                    builder.DataSource,
                    Directory.GetCurrentDirectory());
            }

            return builder.ConnectionString;
        }
        catch (Exception exception) when (exception is ArgumentException
            or FormatException
            or NotSupportedException
            or PathTooLongException)
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} geçersiz.",
                exception);
        }
    }
}
