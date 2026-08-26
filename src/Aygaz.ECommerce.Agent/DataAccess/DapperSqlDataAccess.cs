using System.Data.Common;
using System.Globalization;
using Dapper;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.DataAccess;

public enum RelationalDatabaseProvider
{
    Sqlite,
    SqlServer
}

public sealed class DapperSqlDataAccess : IECommerceDataAccess
{
    private readonly Func<DbConnection> connectionFactory;
    private readonly RelationalDatabaseProvider provider;

    public DapperSqlDataAccess(
        Func<DbConnection> connectionFactory,
        RelationalDatabaseProvider provider)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.provider = provider;
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        return await QuerySingleOrDefaultAsync<CustomerDto>("""
            SELECT Id, FirstName, LastName, Email, Phone, Address, City, CreatedAt
            FROM Customers WHERE Id = @Id;
            """, new { Id = id }, cancellationToken);
    }

    public async Task<CustomerDto?> GetCustomerByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return await QuerySingleOrDefaultAsync<CustomerDto>("""
            SELECT Id, FirstName, LastName, Email, Phone, Address, City, CreatedAt
            FROM Customers WHERE LOWER(Email) = LOWER(@Email);
            """, new { Email = email.Trim() }, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return [];
        string[] tokens = searchTerm.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string sql = "SELECT Id, FirstName, LastName, Email, Phone, Address, City, CreatedAt FROM Customers ORDER BY Id;";
        List<CustomerDto> customers = (await QueryAsync<CustomerDto>(sql, null, cancellationToken)).ToList();
        return customers.Where(customer => tokens.All(token => ($"{customer.FirstName} {customer.LastName}").Contains(token, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public Task<IReadOnlyList<CustomerDto>> SearchCustomersByCityAsync(string city, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(city)
            ? Task.FromResult<IReadOnlyList<CustomerDto>>([])
            : QueryAsync<CustomerDto>("""
                SELECT Id, FirstName, LastName, Email, Phone, Address, City, CreatedAt
                FROM Customers WHERE City LIKE @Pattern ORDER BY Id;
                """, new { Pattern = $"%{city.Trim()}%" }, cancellationToken);

    public Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(CancellationToken cancellationToken = default) =>
        QueryAsync<CustomerDto>("SELECT Id, FirstName, LastName, Email, Phone, Address, City, CreatedAt FROM Customers ORDER BY Id;", null, cancellationToken);

    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default) =>
        id <= 0 ? null : await QuerySingleOrDefaultAsync<ProductDto>("SELECT Id, Sku, Name, Category, UnitPrice, IsActive FROM Products WHERE Id = @Id;", new { Id = id }, cancellationToken);

    public async Task<ProductDto?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(sku) ? null : await QuerySingleOrDefaultAsync<ProductDto>("SELECT Id, Sku, Name, Category, UnitPrice, IsActive FROM Products WHERE LOWER(Sku) = LOWER(@Sku);", new { Sku = sku.Trim() }, cancellationToken);

    public Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0) return Task.FromResult<IReadOnlyList<ProductDto>>([]);
        string pattern = $"%{EscapeLikePattern(query.Trim())}%";
        string take = provider == RelationalDatabaseProvider.SqlServer ? "TOP (@MaxResults)" : "";
        string limit = provider == RelationalDatabaseProvider.Sqlite ? "LIMIT @MaxResults" : "";
        return QueryAsync<ProductDto>($"SELECT {take} Id, Sku, Name, Category, UnitPrice, IsActive FROM Products WHERE Name LIKE @Pattern ESCAPE '\\' OR Sku LIKE @Pattern ESCAPE '\\' OR Category LIKE @Pattern ESCAPE '\\' ORDER BY Id {limit};", new { Pattern = pattern, MaxResults = maxResults }, cancellationToken);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int id, CancellationToken cancellationToken = default) =>
        id <= 0 ? null : await QuerySingleOrDefaultAsync<OrderDto>(OrderSelect("WHERE Id = @Id"), new { Id = id }, cancellationToken);

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(orderNumber) ? null : await QuerySingleOrDefaultAsync<OrderDto>(OrderSelect("WHERE LOWER(OrderNumber) = LOWER(@OrderNumber)"), new { OrderNumber = orderNumber.Trim() }, cancellationToken);

    public Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(int customerId, int maxResults, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0 || maxResults <= 0) return Task.FromResult<IReadOnlyList<OrderDto>>([]);
        string take = provider == RelationalDatabaseProvider.SqlServer ? "TOP (@MaxResults)" : "";
        string limit = provider == RelationalDatabaseProvider.Sqlite ? "LIMIT @MaxResults" : "";
        return QueryAsync<OrderDto>($"{OrderSelect($"WHERE CustomerId = @CustomerId ORDER BY OrderDate DESC, Id DESC {limit}", take)}", new { CustomerId = customerId, MaxResults = maxResults }, cancellationToken);
    }

    public async Task<OrderDto?> GetLatestCustomerOrderAsync(int customerId, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return null;
        }

        string take = provider == RelationalDatabaseProvider.SqlServer ? "TOP (1)" : "";
        string limit = provider == RelationalDatabaseProvider.Sqlite ? "LIMIT 1" : "";
        return await QuerySingleOrDefaultAsync<OrderDto>(
            OrderSelect($"WHERE CustomerId = @CustomerId ORDER BY OrderDate DESC, Id DESC {limit}", take),
            new { CustomerId = customerId },
            cancellationToken);
    }

    public async Task<OrderDetailDto?> GetCustomerOrderDetailAsync(int orderId, CancellationToken cancellationToken = default)
    {
        OrderDto? order = await GetOrderByIdAsync(orderId, cancellationToken);
        if (order is null) return null;
        const string sql = """
            SELECT oi.Id, oi.ProductId, p.Sku, p.Name AS ProductName,
                   oi.Quantity, oi.UnitPrice, oi.UnitPrice * oi.Quantity AS LineTotal
            FROM OrderItems oi INNER JOIN Products p ON p.Id = oi.ProductId
            WHERE oi.CustomerOrderId = @OrderId ORDER BY oi.Id;
            """;
        IReadOnlyList<OrderItemDto> items = await QueryAsync<OrderItemDto>(sql, new { OrderId = orderId }, cancellationToken);
        return new OrderDetailDto(order, items);
    }

    public Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(int productId, int maxResults, CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || maxResults <= 0) return Task.FromResult<IReadOnlyList<InventoryDto>>([]);
        string take = provider == RelationalDatabaseProvider.SqlServer ? "TOP (@MaxResults)" : "";
        string limit = provider == RelationalDatabaseProvider.Sqlite ? "LIMIT @MaxResults" : "";
        return QueryAsync<InventoryDto>($"SELECT {take} Id, ProductId, LocationCode, LocationName, QuantityAvailable, ReorderLevel, UpdatedAt FROM InventoryRecords WHERE ProductId = @ProductId ORDER BY LocationCode, Id {limit};", new { ProductId = productId, MaxResults = maxResults }, cancellationToken);
    }

    public async Task<long?> GetTotalAvailableStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) return null;
        long? result = await QuerySingleOrDefaultAsync<long?>("SELECT SUM(QuantityAvailable) FROM InventoryRecords WHERE ProductId = @ProductId;", new { ProductId = productId }, cancellationToken);
        return result;
    }

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var aggregate = await QuerySingleOrDefaultAsync<SalesAggregate>(SalesAggregateSql(null), DateParameters(fromDate, toDate), cancellationToken);
        int orderCount = await QuerySingleOrDefaultAsync<int>(EligibleOrdersSql("COUNT(*)"), DateParameters(fromDate, toDate), cancellationToken);
        decimal revenue = aggregate?.Revenue ?? 0m;
        return new SalesSummaryDto(revenue, orderCount, aggregate?.Quantity ?? 0, orderCount == 0 ? 0 : decimal.Round(revenue / orderCount, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(DateOnly fromDate, DateOnly toDate, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return [];
        string take = provider == RelationalDatabaseProvider.SqlServer ? "TOP (@Limit)" : "";
        string limitSql = provider == RelationalDatabaseProvider.Sqlite ? "LIMIT @Limit" : "";
        return await QueryAsync<TopSellingProductDto>($"""
            SELECT {take} p.Sku, p.Name AS ProductName, SUM(oi.Quantity) AS QuantitySold, SUM(oi.UnitPrice * oi.Quantity) AS Revenue
            FROM CustomerOrders o INNER JOIN OrderItems oi ON oi.CustomerOrderId = o.Id INNER JOIN Products p ON p.Id = oi.ProductId
            WHERE o.Status <> @Cancelled AND o.OrderDate >= @FromDate AND o.OrderDate < @ToDate
            GROUP BY p.Sku, p.Name ORDER BY QuantitySold DESC, Revenue DESC, p.Sku {limitSql};
            """, DateParameters(fromDate, toDate, limit), cancellationToken);
    }

    public async Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(int customerId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0 || await GetCustomerByIdAsync(customerId, cancellationToken) is null) return null;
        SalesAggregate? aggregate = await QuerySingleOrDefaultAsync<SalesAggregate>(SalesAggregateSql("AND o.CustomerId = @CustomerId"), DateParameters(fromDate, toDate, customerId: customerId), cancellationToken);
        int orderCount = await QuerySingleOrDefaultAsync<int>(EligibleOrdersSql("COUNT(*)", "AND CustomerId = @CustomerId"), DateParameters(fromDate, toDate, customerId: customerId), cancellationToken);
        return new CustomerPurchaseSummaryDto(orderCount, aggregate?.Revenue ?? 0m, aggregate?.Quantity ?? 0);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        await using DbConnection connection = connectionFactory();
        await connection.OpenAsync(cancellationToken);
        CommandDefinition command = new(sql, parameters, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<T>(command)).AsList();
    }

    private async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        await using DbConnection connection = connectionFactory();
        await connection.OpenAsync(cancellationToken);
        CommandDefinition command = new(sql, parameters, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(command);
    }

    private string OrderSelect(string whereAndOrder, string take = "") => $"SELECT {take} Id, OrderNumber, CustomerId, OrderDate, Status, TotalAmount FROM CustomerOrders {whereAndOrder};";
    private string EligibleOrdersSql(string projection, string extra = "") => $"SELECT {projection} FROM CustomerOrders WHERE Status <> @Cancelled AND OrderDate >= @FromDate AND OrderDate < @ToDate {extra};";
    private string SalesAggregateSql(string? extra) => $"SELECT SUM(oi.UnitPrice * oi.Quantity) AS Revenue, SUM(oi.Quantity) AS Quantity FROM CustomerOrders o INNER JOIN OrderItems oi ON oi.CustomerOrderId = o.Id WHERE o.Status <> @Cancelled AND o.OrderDate >= @FromDate AND o.OrderDate < @ToDate {extra};";

    private static object DateParameters(DateOnly fromDate, DateOnly toDate, int? customerId = null, int? limit = null) => new
    {
        FromDate = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        ToDate = toDate == DateOnly.MaxValue ? DateTime.MaxValue : toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        Cancelled = nameof(OrderStatus.Cancelled),
        CustomerId = customerId,
        Limit = limit
    };

    private static string EscapeLikePattern(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
    private sealed class SalesAggregate
    {
        public decimal? Revenue { get; set; }

        public long? Quantity { get; set; }
    }
}
