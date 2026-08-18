using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Aygaz.ECommerce.Agent.Configuration;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class SalesAnalyticsDataIntegrationTests
{
    private static readonly DateOnly FullFromDate = new(2026, 2, 1);
    private static readonly DateOnly FullToDate = new(2026, 3, 6);

    [Fact]
    public async Task InitializeAsync_WithSeparateContexts_SeedsExactlyFortyEightOrderItemsOnce()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);

        await using (var firstContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(firstContext).InitializeAsync();
            Assert.Equal(48, await firstContext.OrderItems.CountAsync());
        }

        await using (var secondContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(secondContext).InitializeAsync();

            Assert.Equal(48, await secondContext.OrderItems.CountAsync());
            Assert.Equal(
                48,
                await secondContext.OrderItems
                    .Select(item => new { item.CustomerOrderId, item.ProductId })
                    .Distinct()
                    .CountAsync());
            Assert.Equal(12, await secondContext.Customers.CountAsync());
            Assert.Equal(24, await secondContext.CustomerOrders.CountAsync());
            Assert.Equal(12, await secondContext.Products.CountAsync());
            Assert.Equal(16, await secondContext.InventoryRecords.CountAsync());
        }
    }

    [Fact]
    public async Task SeededOrderItems_MatchEveryOrderHeaderAndExactProductPattern()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));

        CustomerOrder[] orders = await context.CustomerOrders
            .AsNoTracking()
            .Include(order => order.OrderItems)
            .ThenInclude(item => item.Product)
            .OrderBy(order => order.OrderNumber)
            .ToArrayAsync();

        Assert.Equal(24, orders.Length);
        Assert.All(
            orders,
            order =>
            {
                Assert.Equal(2, order.OrderItems.Count);
                Assert.Equal(
                    order.TotalAmount,
                    order.OrderItems.Sum(item => item.Quantity * item.UnitPrice));

                OrderItem common = Assert.Single(
                    order.OrderItems,
                    item => item.Product.Sku == "AYG-DEMO-PRD-001");
                Assert.Equal(3, common.Quantity);
                Assert.Equal(10.00m, common.UnitPrice);
            });

        var expectedSecondary = new Dictionary<string, (string Sku, decimal UnitPrice)>
        {
            ["AYG-DEMO-1001"] = ("AYG-DEMO-PRD-002", 1220.00m),
            ["AYG-DEMO-1002"] = ("AYG-DEMO-PRD-003", 310.50m),
            ["AYG-DEMO-1003"] = ("AYG-DEMO-PRD-004", 750.25m),
            ["AYG-DEMO-1004"] = ("AYG-DEMO-PRD-005", 880.75m),
            ["AYG-DEMO-1005"] = ("AYG-DEMO-PRD-006", 185.00m),
            ["AYG-DEMO-1006"] = ("AYG-DEMO-PRD-007", 610.90m),
            ["AYG-DEMO-1007"] = ("AYG-DEMO-PRD-008", 445.40m),
            ["AYG-DEMO-1008"] = ("AYG-DEMO-PRD-009", 1090.00m),
            ["AYG-DEMO-1009"] = ("AYG-DEMO-PRD-010", 150.75m),
            ["AYG-DEMO-1010"] = ("AYG-DEMO-PRD-011", 795.30m),
            ["AYG-DEMO-1011"] = ("AYG-DEMO-PRD-012", 960.00m),
            ["AYG-DEMO-1012"] = ("AYG-DEMO-PRD-002", 275.60m),
            ["AYG-DEMO-1013"] = ("AYG-DEMO-PRD-003", 530.45m),
            ["AYG-DEMO-1014"] = ("AYG-DEMO-PRD-004", 1420.00m),
            ["AYG-DEMO-1015"] = ("AYG-DEMO-PRD-005", 245.10m),
            ["AYG-DEMO-1016"] = ("AYG-DEMO-PRD-006", 680.80m),
            ["AYG-DEMO-1017"] = ("AYG-DEMO-PRD-007", 400.00m),
            ["AYG-DEMO-1018"] = ("AYG-DEMO-PRD-008", 845.55m),
            ["AYG-DEMO-1019"] = ("AYG-DEMO-PRD-009", 1290.20m),
            ["AYG-DEMO-1020"] = ("AYG-DEMO-PRD-010", 165.90m),
            ["AYG-DEMO-1021"] = ("AYG-DEMO-PRD-011", 650.00m),
            ["AYG-DEMO-1022"] = ("AYG-DEMO-PRD-012", 1015.35m),
            ["AYG-DEMO-1023"] = ("AYG-DEMO-PRD-002", 490.00m),
            ["AYG-DEMO-1024"] = ("AYG-DEMO-PRD-003", 730.65m)
        };

        foreach (CustomerOrder order in orders)
        {
            (string expectedSku, decimal expectedPrice) =
                expectedSecondary[order.OrderNumber];
            OrderItem secondary = Assert.Single(
                order.OrderItems,
                item => item.Product.Sku != "AYG-DEMO-PRD-001");
            Assert.Equal(expectedSku, secondary.Product.Sku);
            Assert.Equal(1, secondary.Quantity);
            Assert.Equal(expectedPrice, secondary.UnitPrice);
        }
    }

    [Fact]
    public async Task DatabaseModel_ConfiguresOrderItemPrecisionUniqueKeysAndRequiredRelationships()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));

        var entity = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            context.Model.FindEntityType(typeof(OrderItem)));
        var unitPrice = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IProperty>(
            entity.FindProperty(nameof(OrderItem.UnitPrice)));

        Assert.Equal(18, unitPrice.GetPrecision());
        Assert.Equal(2, unitPrice.GetScale());
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(
                    [nameof(OrderItem.CustomerOrderId), nameof(OrderItem.ProductId)]));

        var orderForeignKey = Assert.Single(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(OrderItem.CustomerOrderId)]));
        var productForeignKey = Assert.Single(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(OrderItem.ProductId)]));
        Assert.True(orderForeignKey.IsRequired);
        Assert.True(productForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, orderForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, productForeignKey.DeleteBehavior);
    }

    [Fact]
    public async Task Database_RejectsDuplicateOrderProductLine()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        var ids = await GetOrderAndProductIdsAsync(
            context,
            "AYG-DEMO-1001",
            "AYG-DEMO-PRD-001");
        context.OrderItems.Add(CreateOrderItem(ids.OrderId, ids.ProductId));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Database_RejectsMissingOrderOrProductForeignKey(bool missingOrder)
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        var ids = await GetOrderAndProductIdsAsync(
            context,
            "AYG-DEMO-1001",
            "AYG-DEMO-PRD-012");
        context.OrderItems.Add(CreateOrderItem(
            missingOrder ? int.MaxValue : ids.OrderId,
            missingOrder ? ids.ProductId : int.MaxValue));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task Database_RejectsNonPositiveQuantityOrNegativeUnitPrice(
        int quantity,
        int unitPrice)
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        var ids = await GetOrderAndProductIdsAsync(
            context,
            "AYG-DEMO-1001",
            "AYG-DEMO-PRD-012");
        OrderItem item = CreateOrderItem(ids.OrderId, ids.ProductId);
        item.Quantity = quantity;
        item.UnitPrice = unitPrice;
        context.OrderItems.Add(item);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task GetSalesSummaryAsync_FullRange_ReturnsExactAggregatesAndExcludesCancelled()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        SalesAnalyticsService service = CreateService(context);

        SalesSummaryDto result =
            await service.GetSalesSummaryAsync(FullFromDate, FullToDate);

        Assert.Equal(14946.40m, result.TotalRevenue);
        Assert.Equal(20, result.OrderCount);
        Assert.Equal(80L, result.ItemsSold);
        Assert.Equal(747.32m, result.AverageOrderValue);

        OrderItem[] cancelledItems = await context.OrderItems
            .AsNoTracking()
            .Where(item => item.CustomerOrder.Status == OrderStatus.Cancelled)
            .ToArrayAsync();
        Assert.Equal(8, cancelledItems.Length);
        Assert.Equal(1912.05m, cancelledItems.Sum(item => item.Quantity * item.UnitPrice));
        Assert.Equal(16, cancelledItems.Sum(item => item.Quantity));
        Assert.Equal(
            4,
            await context.CustomerOrders.CountAsync(
                order => order.Status == OrderStatus.Cancelled));
    }

    [Fact]
    public async Task GetSalesSummaryAsync_LastThirtyDemoDays_ReturnsExactAggregates()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        SalesAnalyticsService service = CreateService(context);

        SalesSummaryDto result = await service.GetSalesSummaryAsync(
            new DateOnly(2026, 2, 5),
            FullToDate);

        Assert.Equal(13006.00m, result.TotalRevenue);
        Assert.Equal(17, result.OrderCount);
        Assert.Equal(68L, result.ItemsSold);
        Assert.Equal(765.06m, result.AverageOrderValue);
    }

    [Fact]
    public async Task GetSalesSummaryAsync_EmptyRange_ReturnsZeros()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        SalesAnalyticsService service = CreateService(context);

        SalesSummaryDto result = await service.GetSalesSummaryAsync(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31));

        Assert.Equal(new SalesSummaryDto(0m, 0, 0L, 0m), result);
    }

    [Fact]
    public async Task GetTopSellingProductsAsync_FullRange_ReturnsExactDeterministicTopFive()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        SalesAnalyticsService service = CreateService(context);

        IReadOnlyList<TopSellingProductDto> result =
            await service.GetTopSellingProductsAsync(FullFromDate, FullToDate, 5);

        Assert.Equal(
            new[]
            {
                new TopSellingProductDto("AYG-DEMO-PRD-001", "Demo Product Alpha", 60L, 600.00m),
                new TopSellingProductDto("AYG-DEMO-PRD-002", "Demo Product Beta", 3L, 1985.60m),
                new TopSellingProductDto("AYG-DEMO-PRD-009", "Demo Product Iota", 2L, 2380.20m),
                new TopSellingProductDto("AYG-DEMO-PRD-004", "Demo Product Delta", 2L, 2170.25m),
                new TopSellingProductDto("AYG-DEMO-PRD-012", "Demo Product Mu", 2L, 1975.35m)
            },
            result);
        Assert.Equal(
            "AYG-DEMO-PRD-001",
            Assert.Single(
                await service.GetTopSellingProductsAsync(FullFromDate, FullToDate, 1)).Sku);
    }

    [Fact]
    public async Task GetTopSellingProductsAsync_EmptyRange_ReturnsEmptyList()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        SalesAnalyticsService service = CreateService(context);

        IReadOnlyList<TopSellingProductDto> result =
            await service.GetTopSellingProductsAsync(
                new DateOnly(2026, 7, 1),
                new DateOnly(2026, 7, 31),
                5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCustomerPurchaseSummaryAsync_DistinguishesExactCancelledZeroAndMissing()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        SalesAnalyticsService service = CreateService(context);
        int ahmetId = await GetCustomerIdAsync(context, "ahmet.yilmaz@example.com");
        int burakId = await GetCustomerIdAsync(context, "burak.yildiz@example.com");

        CustomerPurchaseSummaryDto ahmet = Assert.IsType<CustomerPurchaseSummaryDto>(
            await service.GetCustomerPurchaseSummaryAsync(
                ahmetId,
                new DateOnly(2025, 12, 7),
                FullToDate));
        CustomerPurchaseSummaryDto burak = Assert.IsType<CustomerPurchaseSummaryDto>(
            await service.GetCustomerPurchaseSummaryAsync(
                burakId,
                FullFromDate,
                FullToDate));

        Assert.Equal(new CustomerPurchaseSummaryDto(3, 2941.00m, 12L), ahmet);
        Assert.Equal(new CustomerPurchaseSummaryDto(0, 0m, 0L), burak);
        Assert.Null(await service.GetCustomerPurchaseSummaryAsync(
            int.MaxValue,
            FullFromDate,
            FullToDate));
    }

    [Fact]
    public async Task SalesAnalyticsService_InvalidRangesLimitsAndCustomerId_FailFast()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        SalesAnalyticsService service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetSalesSummaryAsync(FullToDate, FullFromDate));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetSalesSummaryAsync(
                new DateOnly(2025, 3, 5),
                FullToDate));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetTopSellingProductsAsync(
                FullFromDate,
                FullToDate,
                0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetTopSellingProductsAsync(
                FullFromDate,
                FullToDate,
                11));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetCustomerPurchaseSummaryAsync(
                0,
                FullFromDate,
                FullToDate));
    }

    [Fact]
    public async Task SalesAnalyticsService_InvalidConfiguredLimits_FailBeforeDatabaseQuery()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));

        var invalidRangeService = new SalesAnalyticsService(
            context,
            Options.Create(new CommerceOptions
            {
                MaximumAnalysisRangeDays = 0,
                MaximumTopProducts = 10
            }));
        var invalidTopService = new SalesAnalyticsService(
            context,
            Options.Create(new CommerceOptions
            {
                MaximumAnalysisRangeDays = 366,
                MaximumTopProducts = 11
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => invalidRangeService.GetSalesSummaryAsync(
                FullFromDate,
                FullToDate));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => invalidTopService.GetTopSellingProductsAsync(
                FullFromDate,
                FullToDate,
                5));
    }

    private static OrderItem CreateOrderItem(int orderId, int productId)
    {
        return new OrderItem
        {
            CustomerOrderId = orderId,
            ProductId = productId,
            Quantity = 1,
            UnitPrice = 1m
        };
    }

    private static SalesAnalyticsService CreateService(ECommerceDbContext context)
    {
        return new SalesAnalyticsService(
            context,
            Options.Create(new CommerceOptions
            {
                CurrencyCode = "TRY",
                AnalyticsReferenceDate = "2026-03-06",
                MaximumAnalysisRangeDays = 366,
                MaximumTopProducts = 10
            }));
    }

    private static async Task<(int OrderId, int ProductId)> GetOrderAndProductIdsAsync(
        ECommerceDbContext context,
        string orderNumber,
        string sku)
    {
        int orderId = await context.CustomerOrders
            .Where(order => order.OrderNumber == orderNumber)
            .Select(order => order.Id)
            .SingleAsync();
        int productId = await context.Products
            .Where(product => product.Sku == sku)
            .Select(product => product.Id)
            .SingleAsync();
        return (orderId, productId);
    }

    private static Task<int> GetCustomerIdAsync(
        ECommerceDbContext context,
        string email)
    {
        return context.Customers
            .Where(customer => customer.Email == email)
            .Select(customer => customer.Id)
            .SingleAsync();
    }

    private static DbContextOptions<ECommerceDbContext> CreateOptions(
        SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<ECommerceDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<SqliteConnection> OpenInitializedDatabaseAsync()
    {
        SqliteConnection connection = await OpenDatabaseAsync();

        try
        {
            DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
            await using var context = new ECommerceDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
