using MongoDB.Bson;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.DataAccess;

internal static class MongoCollectionSetup
{
    public const string Customers = "customers";
    public const string Products = "products";
    public const string Orders = "orders";
    public const string OrderItems = "orderItems";
    public const string Inventory = "inventory";
    public const string OrderAuditLogs = "orderAuditLogs";

    public static IReadOnlyDictionary<string, IReadOnlyList<CreateIndexModel<BsonDocument>>> CreateIndexModels()
    {
        return new Dictionary<string, IReadOnlyList<CreateIndexModel<BsonDocument>>>
        {
            [Customers] =
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("id"),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("email"),
                    new CreateIndexOptions { Unique = true })
            ],
            [Products] =
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("id"),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("sku"),
                    new CreateIndexOptions { Unique = true })
            ],
            [Orders] =
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("id"),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("orderNumber"),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("customerId").Descending("orderDate"))
            ],
            [OrderItems] =
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("id"),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("orderId").Ascending("productId"),
                    new CreateIndexOptions { Unique = true })
            ],
            [Inventory] =
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("id"),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("productId").Ascending("locationCode"),
                    new CreateIndexOptions { Unique = true })
            ],
            [OrderAuditLogs] =
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("orderNumber").Descending("createdAt")),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("orderId").Descending("createdAt"))
            ]
        };
    }
}
