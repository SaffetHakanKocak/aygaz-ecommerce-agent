using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class MongoDummyDataSeederTests
{
    public const string KitchenCylinderSku = "AYG-DEMO-PRD-003";
    public const string KitchenCylinderName = "Mutfak Tupu 12KG";

    [Fact]
    public void Seeder_IncludesKitchenCylinderProductAndStock()
    {
        BsonDocument product = Assert.Single(
            MongoDummyDataSeeder.CreateProducts(),
            document => document["sku"].AsString == KitchenCylinderSku);

        Assert.Equal(2003, product["id"].AsInt32);
        Assert.Equal(KitchenCylinderName, product["name"].AsString);
        Assert.Equal("LPG", product["category"].AsString);
        Assert.Equal(890m, product["unitPrice"].ToDecimal());
        Assert.True(product["isActive"].AsBoolean);

        BsonDocument inventory = Assert.Single(
            MongoDummyDataSeeder.CreateInventory(),
            document => document["productId"].AsInt32 == 2003);

        Assert.Equal("IST-01", inventory["locationCode"].AsString);
        Assert.Equal(80, inventory["quantityAvailable"].AsInt32);
    }

    [Fact]
    public async Task SeededMongo_LookupBySku_ReturnsKitchenCylinder()
    {
        var options = new MongoDbOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "aygaz-ecommerce"
        };

        if (!await CanConnectAsync(options.ConnectionString))
        {
            return;
        }

        var seeder = new MongoDummyDataSeeder(options);
        await seeder.SeedAsync();

        var client = new MongoClient(options.ConnectionString);
        var access = new MongoDataAccess(client.GetDatabase(options.DatabaseName));
        var productService = new ProductService(access);
        var inventoryService = new InventoryService(access);

        ProductDto? product = await productService.GetProductBySkuAsync(KitchenCylinderSku);
        Assert.NotNull(product);
        Assert.Equal(KitchenCylinderSku, product.Sku);
        Assert.Equal(KitchenCylinderName, product.Name);
        Assert.Equal("LPG", product.Category);
        Assert.Equal(890m, product.UnitPrice);

        long? stock = await inventoryService.GetTotalAvailableStockAsync(product.Id);
        Assert.Equal(80, stock);
    }

    private static async Task<bool> CanConnectAsync(string connectionString)
    {
        try
        {
            MongoClientSettings settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            var client = new MongoClient(settings);
            await client.ListDatabaseNamesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
