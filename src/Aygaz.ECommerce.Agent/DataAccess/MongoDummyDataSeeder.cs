using MongoDB.Bson;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.DataAccess;

public sealed class MongoDummyDataSeeder
{
    private readonly MongoDbOptions options;
    private readonly IMongoDatabaseInitializer? initializer;

    public MongoDummyDataSeeder(
        MongoDbOptions options,
        IMongoDatabaseInitializer? initializer = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.initializer = initializer;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("MongoDB bağlantı dizesi boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            throw new InvalidOperationException("MongoDB veritabanı adı boş olamaz.");
        }

        var client = new MongoClient(options.ConnectionString);
        IMongoDatabase database = client.GetDatabase(options.DatabaseName);

        if (initializer is not null)
        {
            await initializer.InitializeAsync(cancellationToken);
        }
        else
        {
            await new MongoDatabaseInitializer(database).InitializeAsync(cancellationToken);
        }

        await UpsertDocumentsAsync(
            database.GetCollection<BsonDocument>(MongoCollectionSetup.Customers),
            "id",
            CreateCustomers(),
            cancellationToken);
        await UpsertDocumentsAsync(
            database.GetCollection<BsonDocument>(MongoCollectionSetup.Products),
            "id",
            CreateProducts(),
            cancellationToken);
        await UpsertDocumentsAsync(
            database.GetCollection<BsonDocument>(MongoCollectionSetup.Orders),
            "id",
            CreateOrders(),
            cancellationToken);
        await UpsertDocumentsAsync(
            database.GetCollection<BsonDocument>(MongoCollectionSetup.OrderItems),
            "id",
            CreateOrderItems(),
            cancellationToken);
        await UpsertDocumentsAsync(
            database.GetCollection<BsonDocument>(MongoCollectionSetup.Inventory),
            "id",
            CreateInventory(),
            cancellationToken);
    }

    internal static IReadOnlyList<ReplaceOneModel<BsonDocument>> CreateUpsertModels(
        string key,
        IEnumerable<BsonDocument> documents)
    {
        return documents
            .Select(document => new ReplaceOneModel<BsonDocument>(
                Builders<BsonDocument>.Filter.Eq(key, document[key]),
                document)
            {
                IsUpsert = true
            })
            .ToList();
    }

    private static async Task UpsertDocumentsAsync(
        IMongoCollection<BsonDocument> collection,
        string key,
        IEnumerable<BsonDocument> documents,
        CancellationToken cancellationToken)
    {
        foreach (ReplaceOneModel<BsonDocument> model in CreateUpsertModels(key, documents))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await collection.ReplaceOneAsync(
                model.Filter,
                model.Replacement,
                new ReplaceOptions { IsUpsert = model.IsUpsert },
                cancellationToken);
        }
    }

    private static IReadOnlyList<BsonDocument> CreateCustomers() =>
    [
        new BsonDocument
        {
            ["id"] = 1001,
            ["firstName"] = "Ayse",
            ["lastName"] = "Yilmaz",
            ["email"] = "ayse.yilmaz@example.com",
            ["phone"] = "+90 555 100 1001",
            ["address"] = "Ataturk Caddesi No: 10",
            ["city"] = "Istanbul",
            ["createdAt"] = new BsonDateTime(new DateTime(2025, 1, 15, 9, 0, 0, DateTimeKind.Utc))
        },
        new BsonDocument
        {
            ["id"] = 1002,
            ["firstName"] = "Mehmet",
            ["lastName"] = "Kaya",
            ["email"] = "mehmet.kaya@example.com",
            ["phone"] = "+90 555 100 1002",
            ["address"] = "Inonu Sokak No: 4",
            ["city"] = "Ankara",
            ["createdAt"] = new BsonDateTime(new DateTime(2025, 2, 20, 10, 30, 0, DateTimeKind.Utc))
        }
    ];

    internal static IReadOnlyList<BsonDocument> CreateProducts() =>
    [
        new BsonDocument
        {
            ["id"] = 2001,
            ["sku"] = "AYG-SU-001",
            ["name"] = "Damacana Su 19L",
            ["category"] = "Su",
            ["unitPrice"] = 145.50m,
            ["isActive"] = true,
            ["createdAt"] = new BsonDateTime(new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc))
        },
        new BsonDocument
        {
            ["id"] = 2002,
            ["sku"] = "AYG-GAZ-012",
            ["name"] = "Piknik Tupu 2KG",
            ["category"] = "LPG",
            ["unitPrice"] = 320m,
            ["isActive"] = true,
            ["createdAt"] = new BsonDateTime(new DateTime(2025, 1, 5, 8, 0, 0, DateTimeKind.Utc))
        },
        new BsonDocument
        {
            ["id"] = 2003,
            ["sku"] = "AYG-DEMO-PRD-003",
            ["name"] = "Mutfak Tupu 12KG",
            ["category"] = "LPG",
            ["unitPrice"] = 890m,
            ["isActive"] = true,
            ["createdAt"] = new BsonDateTime(new DateTime(2025, 1, 10, 8, 0, 0, DateTimeKind.Utc))
        }
    ];

    private static IReadOnlyList<BsonDocument> CreateOrders() =>
    [
        new BsonDocument
        {
            ["id"] = 3001,
            ["orderNumber"] = "AYG-2025-0001",
            ["customerId"] = 1001,
            ["orderDate"] = new BsonDateTime(new DateTime(2025, 3, 10, 11, 0, 0, DateTimeKind.Utc)),
            ["status"] = "Delivered",
            ["totalAmount"] = 611m
        },
        new BsonDocument
        {
            ["id"] = 3002,
            ["orderNumber"] = "AYG-2025-0002",
            ["customerId"] = 1002,
            ["orderDate"] = new BsonDateTime(new DateTime(2025, 3, 12, 14, 30, 0, DateTimeKind.Utc)),
            ["status"] = "Preparing",
            ["totalAmount"] = 320m
        }
    ];

    private static IReadOnlyList<BsonDocument> CreateOrderItems() =>
    [
        new BsonDocument
        {
            ["id"] = 4001,
            ["orderId"] = 3001,
            ["productId"] = 2001,
            ["quantity"] = 2,
            ["unitPrice"] = 145.50m
        },
        new BsonDocument
        {
            ["id"] = 4002,
            ["orderId"] = 3001,
            ["productId"] = 2002,
            ["quantity"] = 1,
            ["unitPrice"] = 320m
        },
        new BsonDocument
        {
            ["id"] = 4003,
            ["orderId"] = 3002,
            ["productId"] = 2002,
            ["quantity"] = 1,
            ["unitPrice"] = 320m
        }
    ];

    internal static IReadOnlyList<BsonDocument> CreateInventory() =>
    [
        new BsonDocument
        {
            ["id"] = 5001,
            ["productId"] = 2001,
            ["locationCode"] = "IST-01",
            ["locationName"] = "Istanbul Depo",
            ["quantityAvailable"] = 120,
            ["reorderLevel"] = 25,
            ["updatedAt"] = new BsonDateTime(new DateTime(2025, 3, 1, 7, 0, 0, DateTimeKind.Utc))
        },
        new BsonDocument
        {
            ["id"] = 5002,
            ["productId"] = 2002,
            ["locationCode"] = "ANK-01",
            ["locationName"] = "Ankara Depo",
            ["quantityAvailable"] = 45,
            ["reorderLevel"] = 10,
            ["updatedAt"] = new BsonDateTime(new DateTime(2025, 3, 1, 7, 0, 0, DateTimeKind.Utc))
        },
        new BsonDocument
        {
            ["id"] = 5003,
            ["productId"] = 2003,
            ["locationCode"] = "IST-01",
            ["locationName"] = "Istanbul Depo",
            ["quantityAvailable"] = 80,
            ["reorderLevel"] = 15,
            ["updatedAt"] = new BsonDateTime(new DateTime(2025, 3, 1, 7, 0, 0, DateTimeKind.Utc))
        }
    ];
}
