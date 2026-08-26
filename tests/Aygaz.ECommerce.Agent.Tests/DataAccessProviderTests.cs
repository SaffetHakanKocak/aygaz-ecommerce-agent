using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.DataAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class DataAccessProviderTests
{
    [Fact]
    public void DependencyInjection_SelectsMongoByDefault()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["DataAccess:MongoDb:ConnectionString"] = "mongodb://localhost:27017",
            ["DataAccess:MongoDb:DatabaseName"] = "aygaz-test"
        });

        Assert.IsType<MongoDataAccess>(provider.GetRequiredService<IECommerceDataAccess>());
        Assert.NotNull(provider.GetService<IMongoDatabaseInitializer>());
        Assert.Null(provider.GetService<IRelationalDatabaseInitializer>());
    }

    [Fact]
    public void DependencyInjection_SelectsDapperForSqlite()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["DataAccess:Provider"] = "Sqlite",
            ["ConnectionStrings:ECommerceDatabase"] = "Data Source=:memory:"
        });

        Assert.IsType<DapperSqlDataAccess>(provider.GetRequiredService<IECommerceDataAccess>());
        Assert.NotNull(provider.GetService<IRelationalDatabaseInitializer>());
        Assert.Null(provider.GetService<IMongoDatabaseInitializer>());
    }

    [Fact]
    public void DependencyInjection_SelectsDapperForSqlServer()
    {
        using ServiceProvider provider = CreateProvider(new Dictionary<string, string?>
        {
            ["DataAccess:Provider"] = "SqlServer",
            ["DataAccess:SqlServer:ConnectionString"] = "Server=localhost;Database=aygaz-test;Trusted_Connection=True;TrustServerCertificate=True"
        });

        Assert.IsType<DapperSqlDataAccess>(provider.GetRequiredService<IECommerceDataAccess>());
        Assert.NotNull(provider.GetService<IRelationalDatabaseInitializer>());
        Assert.Null(provider.GetService<IMongoDatabaseInitializer>());
    }

    [Fact]
    public void DependencyInjection_RejectsUnknownProvider()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(new Dictionary<string, string?>
            {
                ["DataAccess:Provider"] = "Postgres"
            }).Dispose());

        Assert.Contains("MongoDb, Sqlite veya SqlServer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyInjection_RejectsMissingMongoConnectionString()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CreateProvider(new Dictionary<string, string?>
            {
                ["DataAccess:Provider"] = "MongoDb",
                ["DataAccess:MongoDb:ConnectionString"] = "",
                ["DataAccess:MongoDb:DatabaseName"] = "aygaz-test"
            }).Dispose());

        Assert.Contains("ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MongoIndexSetup_DefinesExpectedUniqueAndLookupIndexes()
    {
        IReadOnlyDictionary<string, IReadOnlyList<CreateIndexModel<BsonDocument>>> indexes =
            MongoCollectionSetup.CreateIndexModels();

        Assert.Contains(MongoCollectionSetup.Customers, indexes.Keys);
        Assert.Contains(MongoCollectionSetup.Products, indexes.Keys);
        Assert.Contains(MongoCollectionSetup.Orders, indexes.Keys);
        Assert.Contains(MongoCollectionSetup.OrderItems, indexes.Keys);
        Assert.Contains(MongoCollectionSetup.Inventory, indexes.Keys);
        Assert.Equal(2, indexes[MongoCollectionSetup.Customers].Count);
        Assert.Equal(2, indexes[MongoCollectionSetup.Products].Count);
        Assert.Equal(3, indexes[MongoCollectionSetup.Orders].Count);
        Assert.Equal(2, indexes[MongoCollectionSetup.OrderItems].Count);
        Assert.Equal(2, indexes[MongoCollectionSetup.Inventory].Count);
        Assert.Equal(2, indexes[MongoCollectionSetup.Customers].Count(index => index.Options?.Unique == true));
        Assert.Equal(2, indexes[MongoCollectionSetup.Products].Count(index => index.Options?.Unique == true));
        Assert.Equal(2, indexes[MongoCollectionSetup.Orders].Count(index => index.Options?.Unique == true));
        Assert.Equal(2, indexes[MongoCollectionSetup.OrderItems].Count(index => index.Options?.Unique == true));
        Assert.Equal(2, indexes[MongoCollectionSetup.Inventory].Count(index => index.Options?.Unique == true));
    }

    [Fact]
    public void MongoSeed_UsesIdempotentUpsertModels()
    {
        BsonDocument[] documents =
        [
            new BsonDocument { ["id"] = 1, ["name"] = "first" },
            new BsonDocument { ["id"] = 2, ["name"] = "second" }
        ];

        IReadOnlyList<ReplaceOneModel<BsonDocument>> models =
            MongoDummyDataSeeder.CreateUpsertModels("id", documents);

        Assert.Equal(2, models.Count);
        Assert.All(models, model => Assert.True(model.IsUpsert));
        Assert.Equal(documents, models.Select(model => model.Replacement));
    }

    private static ServiceProvider CreateProvider(IReadOnlyDictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        ServiceCollection services = new();
        services.AddCustomerData(configuration);
        return services.BuildServiceProvider();
    }
}
