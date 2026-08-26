using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Aygaz.ECommerce.Agent.DataAccess;

public sealed class MongoDataAccess
    : IECommerceDataAccess
{
    private readonly IMongoCollection<BsonDocument> customers;
    private readonly IMongoCollection<BsonDocument> orders;
    private readonly IMongoCollection<BsonDocument> products;
    private readonly IMongoCollection<BsonDocument> orderItems;
    private readonly IMongoCollection<BsonDocument> inventory;

    public MongoDataAccess(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        customers = database.GetCollection<BsonDocument>(MongoCollectionSetup.Customers);
        orders = database.GetCollection<BsonDocument>(MongoCollectionSetup.Orders);
        products = database.GetCollection<BsonDocument>(MongoCollectionSetup.Products);
        orderItems = database.GetCollection<BsonDocument>(MongoCollectionSetup.OrderItems);
        inventory = database.GetCollection<BsonDocument>(MongoCollectionSetup.Inventory);
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        return ToCustomer(await FindOneAsync(customers, Builders<BsonDocument>.Filter.Eq("id", id), cancellationToken));
    }

    public async Task<CustomerDto?> GetCustomerByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var filter = Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{RegexEscape(email.Trim())}$", "i"));
        return ToCustomer(await FindOneAsync(customers, filter, cancellationToken));
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return [];
        string[] tokens = searchTerm.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filters = tokens.Select(token => Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Regex("firstName", new BsonRegularExpression(RegexEscape(token), "i")),
            Builders<BsonDocument>.Filter.Regex("lastName", new BsonRegularExpression(RegexEscape(token), "i"))));
        List<BsonDocument> documents = await customers.Find(Builders<BsonDocument>.Filter.And(filters)).SortBy(x => x["id"]).ToListAsync(cancellationToken);
        return documents.Select(ToCustomer).OfType<CustomerDto>().ToList();
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersByCityAsync(string city, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(city)) return [];
        var filter = Builders<BsonDocument>.Filter.Regex("city", new BsonRegularExpression(RegexEscape(city.Trim()), "i"));
        List<BsonDocument> documents = await customers.Find(filter).SortBy(x => x["id"]).ToListAsync(cancellationToken);
        return documents.Select(ToCustomer).OfType<CustomerDto>().ToList();
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(CancellationToken cancellationToken = default)
    {
        List<BsonDocument> documents = await customers.Find(FilterDefinition<BsonDocument>.Empty).SortBy(x => x["id"]).ToListAsync(cancellationToken);
        return documents.Select(ToCustomer).OfType<CustomerDto>().ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        return ToProduct(await FindOneAsync(products, Builders<BsonDocument>.Filter.Eq("id", id), cancellationToken));
    }

    public async Task<ProductDto?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        var filter = Builders<BsonDocument>.Filter.Regex("sku", new BsonRegularExpression($"^{RegexEscape(sku.Trim())}$", "i"));
        return ToProduct(await FindOneAsync(products, filter, cancellationToken));
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0) return [];
        string pattern = RegexEscape(query.Trim());
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression(pattern, "i")),
            Builders<BsonDocument>.Filter.Regex("sku", new BsonRegularExpression(pattern, "i")),
            Builders<BsonDocument>.Filter.Regex("category", new BsonRegularExpression(pattern, "i")));
        List<BsonDocument> documents = await products.Find(filter).SortBy(x => x["id"]).Limit(maxResults).ToListAsync(cancellationToken);
        return documents.Select(ToProduct).OfType<ProductDto>().ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        return ToOrder(await FindOneAsync(orders, Builders<BsonDocument>.Filter.Eq("id", id), cancellationToken));
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) return null;
        var filter = Builders<BsonDocument>.Filter.Regex("orderNumber", new BsonRegularExpression($"^{RegexEscape(orderNumber.Trim())}$", "i"));
        return ToOrder(await FindOneAsync(orders, filter, cancellationToken));
    }

    public async Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(int customerId, int maxResults, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0 || maxResults <= 0) return [];
        List<BsonDocument> documents = await orders.Find(Builders<BsonDocument>.Filter.Eq("customerId", customerId))
            .SortByDescending(x => x["orderDate"]).ThenByDescending(x => x["id"]).Limit(maxResults).ToListAsync(cancellationToken);
        return documents.Select(ToOrder).OfType<OrderDto>().ToList();
    }

    public async Task<OrderDto?> GetLatestCustomerOrderAsync(int customerId, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) return null;
        BsonDocument? document = await orders.Find(Builders<BsonDocument>.Filter.Eq("customerId", customerId))
            .SortByDescending(x => x["orderDate"]).ThenByDescending(x => x["id"]).FirstOrDefaultAsync(cancellationToken);
        return ToOrder(document);
    }

    public async Task<OrderDetailDto?> GetCustomerOrderDetailAsync(int orderId, CancellationToken cancellationToken = default)
    {
        OrderDto? order = await GetOrderByIdAsync(orderId, cancellationToken);
        if (order is null) return null;
        List<BsonDocument> items = await orderItems.Find(Builders<BsonDocument>.Filter.Eq("orderId", orderId)).SortBy(x => x["id"]).ToListAsync(cancellationToken);
        List<int> productIds = items.Select(x => x.GetValue("productId").AsInt32).Distinct().ToList();
        List<BsonDocument> productDocuments = await products.Find(Builders<BsonDocument>.Filter.In("id", productIds)).ToListAsync(cancellationToken);
        var productMap = productDocuments.ToDictionary(x => x["id"].AsInt32);
        var itemDtos = items.Select(item =>
        {
            int productId = item["productId"].AsInt32;
            productMap.TryGetValue(productId, out BsonDocument? product);
            decimal unitPrice = item["unitPrice"].ToDecimal();
            return new OrderItemDto(item["id"].AsInt32, productId, product?.GetValue("sku", "").AsString ?? "", product?.GetValue("name", "").AsString ?? "", item["quantity"].AsInt32, unitPrice, unitPrice * item["quantity"].AsInt32);
        }).ToList();
        return new OrderDetailDto(order, itemDtos);
    }

    public async Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(int productId, int maxResults, CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || maxResults <= 0) return [];
        List<BsonDocument> documents = await inventory.Find(Builders<BsonDocument>.Filter.Eq("productId", productId)).SortBy(x => x["locationCode"]).ThenBy(x => x["id"]).Limit(maxResults).ToListAsync(cancellationToken);
        return documents.Select(ToInventory).OfType<InventoryDto>().ToList();
    }

    public async Task<long?> GetTotalAvailableStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) return null;
        List<BsonDocument> documents = await inventory.Find(Builders<BsonDocument>.Filter.Eq("productId", productId)).ToListAsync(cancellationToken);
        return documents.Count == 0 ? null : documents.Sum(x => (long)x["quantityAvailable"].AsInt32);
    }

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SaleLine> lines = await GetSaleLinesAsync(fromDate, toDate, null, cancellationToken);
        int orderCount = lines.Select(x => x.OrderId).Distinct().Count();
        decimal revenue = lines.Sum(x => x.Revenue);
        return new SalesSummaryDto(revenue, orderCount, lines.Sum(x => (long)x.Quantity), orderCount == 0 ? 0 : decimal.Round(revenue / orderCount, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(DateOnly fromDate, DateOnly toDate, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return [];
        IReadOnlyList<SaleLine> lines = await GetSaleLinesAsync(fromDate, toDate, null, cancellationToken);
        return lines.GroupBy(x => new { x.Sku, x.ProductName }).Select(group => new TopSellingProductDto(group.Key.Sku, group.Key.ProductName, group.Sum(x => (long)x.Quantity), group.Sum(x => x.Revenue))).OrderByDescending(x => x.QuantitySold).ThenByDescending(x => x.Revenue).ThenBy(x => x.Sku, StringComparer.Ordinal).Take(limit).ToList();
    }

    public async Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(int customerId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0 || await GetCustomerByIdAsync(customerId, cancellationToken) is null) return null;
        IReadOnlyList<SaleLine> lines = await GetSaleLinesAsync(fromDate, toDate, customerId, cancellationToken);
        return new CustomerPurchaseSummaryDto(lines.Select(x => x.OrderId).Distinct().Count(), lines.Sum(x => x.Revenue), lines.Sum(x => (long)x.Quantity));
    }

    private async Task<IReadOnlyList<SaleLine>> GetSaleLinesAsync(DateOnly fromDate, DateOnly toDate, int? customerId, CancellationToken cancellationToken)
    {
        DateTime from = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime to = toDate == DateOnly.MaxValue ? DateTime.MaxValue : toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var filters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Gte("orderDate", from),
            Builders<BsonDocument>.Filter.Lt("orderDate", to),
            Builders<BsonDocument>.Filter.Ne("status", nameof(OrderStatus.Cancelled))
        };
        if (customerId.HasValue) filters.Add(Builders<BsonDocument>.Filter.Eq("customerId", customerId.Value));
        List<BsonDocument> orderDocuments = await orders.Find(Builders<BsonDocument>.Filter.And(filters)).ToListAsync(cancellationToken);
        var orderMap = orderDocuments.ToDictionary(x => x["id"].AsInt32);
        List<BsonDocument> itemDocuments = await orderItems.Find(Builders<BsonDocument>.Filter.In("orderId", orderMap.Keys)).ToListAsync(cancellationToken);
        List<BsonDocument> productDocuments = await products.Find(Builders<BsonDocument>.Filter.In("id", itemDocuments.Select(x => x["productId"].AsInt32).Distinct())).ToListAsync(cancellationToken);
        var productMap = productDocuments.ToDictionary(x => x["id"].AsInt32);
        return itemDocuments.Select(item =>
        {
            BsonDocument product = productMap[item["productId"].AsInt32];
            return new SaleLine(item["orderId"].AsInt32, product["sku"].AsString, product["name"].AsString, item["quantity"].AsInt32, item["unitPrice"].ToDecimal() * item["quantity"].AsInt32);
        }).ToList();
    }

    private static async Task<BsonDocument?> FindOneAsync(IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken) => await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

    private static CustomerDto? ToCustomer(BsonDocument? document) => document is null ? null : new CustomerDto(document["id"].AsInt32, document["firstName"].AsString, document["lastName"].AsString, document["email"].AsString, document.GetValue("phone", BsonNull.Value).IsBsonNull ? null : document["phone"].AsString, document.GetValue("address", BsonNull.Value).IsBsonNull ? null : document["address"].AsString, document.GetValue("city", BsonNull.Value).IsBsonNull ? null : document["city"].AsString, document["createdAt"].ToUniversalTime());

    private static ProductDto? ToProduct(BsonDocument? document) => document is null ? null : new ProductDto(document["id"].AsInt32, document["sku"].AsString, document["name"].AsString, document["category"].AsString, document["unitPrice"].ToDecimal(), document["isActive"].AsBoolean);

    private static OrderDto? ToOrder(BsonDocument? document) => document is null ? null : new OrderDto(document["id"].AsInt32, document["orderNumber"].AsString, document["customerId"].AsInt32, document["orderDate"].ToUniversalTime(), Enum.Parse<OrderStatus>(document["status"].AsString, true), document["totalAmount"].ToDecimal());

    private static InventoryDto? ToInventory(BsonDocument? document) => document is null ? null : new InventoryDto(document["id"].AsInt32, document["productId"].AsInt32, document["locationCode"].AsString, document["locationName"].AsString, document["quantityAvailable"].AsInt32, document["reorderLevel"].AsInt32, document["updatedAt"].ToUniversalTime());

    private static string RegexEscape(string value) => global::System.Text.RegularExpressions.Regex.Escape(value);

    private sealed record SaleLine(int OrderId, string Sku, string ProductName, int Quantity, decimal Revenue);
}
