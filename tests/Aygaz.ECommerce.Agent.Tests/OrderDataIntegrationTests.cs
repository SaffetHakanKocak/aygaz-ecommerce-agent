using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class OrderDataIntegrationTests
{
    [Fact]
    public async Task InitializeAsync_WhenCalledWithSeparateContexts_SeedsExactlyTwentyFourOrdersOnce()
    {
        await using var connection = await OpenDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);

        int firstCount;
        await using (var firstContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(firstContext).InitializeAsync();
            firstCount = await firstContext.CustomerOrders.CountAsync();
        }

        int secondCount;
        int distinctOrderNumberCount;
        await using (var secondContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(secondContext).InitializeAsync();
            secondCount = await secondContext.CustomerOrders.CountAsync();
            distinctOrderNumberCount = await secondContext.CustomerOrders
                .Select(order => order.OrderNumber)
                .Distinct()
                .CountAsync();
        }

        Assert.Equal(24, firstCount);
        Assert.Equal(24, secondCount);
        Assert.Equal(secondCount, distinctOrderNumberCount);
    }

    [Fact]
    public async Task SeededOrders_HaveExpectedCustomerDistributionStatusesAndRelationship()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);

        Customer ahmet = await context.Customers
            .Include(customer => customer.Orders)
            .SingleAsync(customer => customer.Email == "ahmet.yilmaz@example.com");
        Customer burak = await context.Customers
            .Include(customer => customer.Orders)
            .SingleAsync(customer => customer.Email == "burak.yildiz@example.com");
        CustomerOrder[] orders = await context.CustomerOrders
            .AsNoTracking()
            .ToArrayAsync();

        Assert.Equal(4, ahmet.Orders.Count);
        Assert.Empty(burak.Orders);
        Assert.Equal(
            Enum.GetValues<OrderStatus>().OrderBy(status => status),
            orders.Select(order => order.Status).Distinct().OrderBy(status => status));

        int[] customerIds = await context.Customers
            .AsNoTracking()
            .Select(customer => customer.Id)
            .ToArrayAsync();
        Assert.All(orders, order => Assert.Contains(order.CustomerId, customerIds));

        CustomerOrder latestAhmetOrder = Assert.Single(
            ahmet.Orders,
            order => order.OrderNumber == "AYG-DEMO-1004");
        Assert.Equal(OrderStatus.Preparing, latestAhmetOrder.Status);
        Assert.Equal(910.75m, latestAhmetOrder.TotalAmount);
        Assert.Equal(
            new DateTime(2026, 2, 22, 10, 0, 0, DateTimeKind.Utc),
            latestAhmetOrder.OrderDate);
    }

    [Fact]
    public async Task DatabaseModel_ConfiguresOrderNumberAmountAndRequiredCustomerConstraints()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);

        var entity = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            context.Model.FindEntityType(typeof(CustomerOrder)));
        var orderNumber = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IProperty>(
            entity.FindProperty(nameof(CustomerOrder.OrderNumber)));
        var totalAmount = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IProperty>(
            entity.FindProperty(nameof(CustomerOrder.TotalAmount)));

        Assert.Equal(CustomerOrder.MaximumOrderNumberLength, orderNumber.GetMaxLength());
        Assert.Equal(18, totalAmount.GetPrecision());
        Assert.Equal(2, totalAmount.GetScale());
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(CustomerOrder.OrderNumber)]));

        var customerForeignKey = Assert.Single(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CustomerOrder.CustomerId)]));
        Assert.True(customerForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, customerForeignKey.DeleteBehavior);
    }

    [Fact]
    public async Task Database_RejectsDuplicateOrderNumberRegardlessOfAsciiCasing()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);
        int customerId = await context.Customers
            .Where(customer => customer.Email == "ahmet.yilmaz@example.com")
            .Select(customer => customer.Id)
            .SingleAsync();

        context.CustomerOrders.Add(new CustomerOrder
        {
            OrderNumber = "ayg-demo-1001",
            CustomerId = customerId,
            OrderDate = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Status = OrderStatus.Pending,
            TotalAmount = 10.00m
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_RejectsOrderWithMissingCustomer()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);

        context.CustomerOrders.Add(new CustomerOrder
        {
            OrderNumber = "AYG-INVALID-FK",
            CustomerId = int.MaxValue,
            OrderDate = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Status = OrderStatus.Pending,
            TotalAmount = 10.00m
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_RejectsNegativeOrderAmount()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);
        int customerId = await GetCustomerIdAsync(
            context,
            "ahmet.yilmaz@example.com");

        context.CustomerOrders.Add(new CustomerOrder
        {
            OrderNumber = "AYG-DEMO-NEGATIVE",
            CustomerId = customerId,
            OrderDate = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Status = OrderStatus.Pending,
            TotalAmount = -0.01m
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task OrderService_GetByIdAndNumber_ReturnsDtoOrNull()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);
        var service = new OrderService(context);

        OrderDto byNumber = Assert.IsType<OrderDto>(
            await service.GetOrderByNumberAsync("  ayg-demo-1001  "));
        OrderDto byId = Assert.IsType<OrderDto>(
            await service.GetOrderByIdAsync(byNumber.Id));

        Assert.Equal(byNumber, byId);
        Assert.Equal("AYG-DEMO-1001", byNumber.OrderNumber);
        Assert.Equal(OrderStatus.Delivered, byNumber.Status);
        Assert.Equal(1250.00m, byNumber.TotalAmount);
        Assert.Null(await service.GetOrderByIdAsync(0));
        Assert.Null(await service.GetOrderByIdAsync(int.MaxValue));
        Assert.Null(await service.GetOrderByNumberAsync("AYG-DEMO-9999"));
        Assert.Null(await service.GetOrderByNumberAsync("   "));
    }

    [Fact]
    public async Task OrderService_CustomerOrdersAndLatest_AreLimitedOrderedAndHandleNoOrders()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);
        var service = new OrderService(context);
        int ahmetId = await GetCustomerIdAsync(context, "ahmet.yilmaz@example.com");
        int burakId = await GetCustomerIdAsync(context, "burak.yildiz@example.com");

        IReadOnlyList<OrderDto> limitedOrders =
            await service.GetCustomerOrdersAsync(ahmetId, 3);
        OrderDto latest = Assert.IsType<OrderDto>(
            await service.GetLatestCustomerOrderAsync(ahmetId));

        Assert.Equal(
            new[] { "AYG-DEMO-1004", "AYG-DEMO-1003", "AYG-DEMO-1002" },
            limitedOrders.Select(order => order.OrderNumber));
        Assert.Equal("AYG-DEMO-1004", latest.OrderNumber);
        Assert.Equal(latest, limitedOrders[0]);

        Assert.Empty(await service.GetCustomerOrdersAsync(burakId, 5));
        Assert.Null(await service.GetLatestCustomerOrderAsync(burakId));
        Assert.Empty(await service.GetCustomerOrdersAsync(int.MaxValue, 5));
        Assert.Null(await service.GetLatestCustomerOrderAsync(int.MaxValue));
        Assert.Empty(await service.GetCustomerOrdersAsync(ahmetId, 0));
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
