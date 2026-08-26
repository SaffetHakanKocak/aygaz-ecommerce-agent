using System.Data.Common;
using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Aygaz.ECommerce.Agent.Configuration;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class DapperDataAccessContractTests
{
    [Fact]
    public async Task SqliteFixture_CoversLookupOrderInventoryAndAnalytics()
    {
        await using SqliteConnection keepAlive = new("Data Source=file:contract-tests?mode=memory&cache=shared");
        await keepAlive.OpenAsync();
        var initializer = new DapperRelationalDatabaseInitializer(
            () => new SqliteConnection(keepAlive.ConnectionString),
            RelationalDatabaseProvider.Sqlite);
        await initializer.InitializeAsync();
        await SeedAsync(keepAlive);

        var access = new DapperSqlDataAccess(
            () => new SqliteConnection(keepAlive.ConnectionString),
            RelationalDatabaseProvider.Sqlite);

        CustomerDto? customer = await access.GetCustomerByEmailAsync("AHMET@example.com");
        Assert.NotNull(customer);
        Assert.Equal(1, customer.Id);
        Assert.Single(await access.SearchCustomersByNameAsync("Ahmet Yilmaz"));
        ProductDto? product = await access.GetProductBySkuAsync("AYG-001");
        Assert.NotNull(product);
        Assert.Equal(2, await access.GetTotalAvailableStockAsync(product.Id));
        OrderDto? order = await access.GetOrderByNumberAsync("ORD-001");
        Assert.NotNull(order);
        OrderDetailDto? detail = await access.GetCustomerOrderDetailAsync(order.Id);
        Assert.NotNull(detail);
        Assert.Single(detail.Items);
        Assert.Equal(20m, (await access.GetSalesSummaryAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31))).TotalRevenue);
    }

    private static async Task SeedAsync(DbConnection connection)
    {
        const string sql = """
            INSERT INTO Customers (Id, FirstName, LastName, Email, CreatedAt) VALUES (1, 'Ahmet', 'Yilmaz', 'ahmet@example.com', '2026-01-01');
            INSERT INTO Products (Id, Sku, Name, Category, UnitPrice, IsActive, CreatedAt) VALUES (10, 'AYG-001', 'Water', 'Water', 10, 1, '2026-01-01');
            INSERT INTO CustomerOrders (Id, OrderNumber, CustomerId, OrderDate, Status, TotalAmount) VALUES (20, 'ORD-001', 1, '2026-01-02', 'Delivered', 20);
            INSERT INTO OrderItems (Id, CustomerOrderId, ProductId, Quantity, UnitPrice) VALUES (30, 20, 10, 2, 10);
            INSERT INTO InventoryRecords (Id, ProductId, LocationCode, LocationName, QuantityAvailable, ReorderLevel, UpdatedAt) VALUES (40, 10, 'IST', 'Istanbul', 2, 1, '2026-01-01');
            """;
        await connection.ExecuteNonQueryAsync(sql);
    }
}

file static class DbConnectionExtensions
{
    public static async Task ExecuteNonQueryAsync(this DbConnection connection, string sql)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
