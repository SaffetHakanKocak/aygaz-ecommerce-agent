using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} yapılandırması bulunamadı.");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} boş olamaz.");
        }

        string resolvedConnectionString = ResolveConnectionString(connectionString);

        services.AddDbContext<ECommerceDbContext>(options =>
            options.UseSqlite(resolvedConnectionString));
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ConsoleCustomerTestRunner>();

        return services;
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
