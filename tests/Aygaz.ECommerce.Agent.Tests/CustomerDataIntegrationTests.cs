using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class CustomerDataIntegrationTests
{
    [Fact]
    public async Task InitializeAsync_WhenCalledWithSeparateContexts_SeedsExactlyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);

        int firstCount;
        await using (var firstContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(firstContext).InitializeAsync();
            firstCount = await firstContext.Customers.CountAsync();
        }

        int secondCount;
        int distinctEmailCount;
        await using (var secondContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(secondContext).InitializeAsync();
            secondCount = await secondContext.Customers.CountAsync();
            distinctEmailCount = await secondContext.Customers
                .Select(customer => customer.Email)
                .Distinct()
                .CountAsync();
        }

        Assert.Equal(12, firstCount);
        Assert.Equal(12, secondCount);
        Assert.Equal(secondCount, distinctEmailCount);
    }

    [Fact]
    public async Task CustomerService_GetAllAndById_ReturnsSeededCustomerDtos()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);
        var service = new CustomerService(context);

        IReadOnlyList<CustomerDto> customers = await service.GetAllCustomersAsync();

        Assert.Equal(12, customers.Count);
        CustomerDto ahmet = Assert.Single(
            customers,
            customer => customer.Email == "ahmet.yilmaz@example.com");

        CustomerDto customerById = Assert.IsType<CustomerDto>(
            await service.GetCustomerByIdAsync(ahmet.Id));

        Assert.Equal(ahmet, customerById);
    }

    [Fact]
    public async Task CustomerService_GetByEmail_IsCaseInsensitiveAndReturnsNullWhenMissing()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);
        var service = new CustomerService(context);

        CustomerDto customer = Assert.IsType<CustomerDto>(
            await service.GetCustomerByEmailAsync("  AHMET.YILMAZ@EXAMPLE.COM  "));
        CustomerDto? missingCustomer = await service.GetCustomerByEmailAsync("nobody@example.com");

        Assert.Equal("Ahmet", customer.FirstName);
        Assert.Equal("Yılmaz", customer.LastName);
        Assert.Null(missingCustomer);
    }

    [Theory]
    [InlineData("Ahmet", true)]
    [InlineData("ahmet", true)]
    [InlineData("Yılmaz", true)]
    [InlineData("yılmaz", true)]
    [InlineData("Ahmet Yılmaz", true)]
    [InlineData("   ", false)]
    public async Task CustomerService_SearchByName_HandlesExpectedTerms(
        string searchTerm,
        bool shouldFindAhmet)
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);
        var service = new CustomerService(context);

        IReadOnlyList<CustomerDto> customers = await service.SearchCustomersByNameAsync(searchTerm);

        if (!shouldFindAhmet)
        {
            Assert.Empty(customers);
            return;
        }

        CustomerDto customer = Assert.Single(customers);
        Assert.Equal("Ahmet", customer.FirstName);
        Assert.Equal("Yılmaz", customer.LastName);
    }

    [Fact]
    public async Task Database_RejectsDuplicateEmailRegardlessOfAsciiCasing()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);
        await using var context = new ECommerceDbContext(options);

        context.Customers.Add(new Customer
        {
            FirstName = "Test",
            LastName = "Duplicate",
            Email = "AHMET.YILMAZ@EXAMPLE.COM",
            Phone = "000-000-9999 (TEST)",
            City = "Test City",
            CreatedAt = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static DbContextOptions<ECommerceDbContext> CreateOptions(
        SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<ECommerceDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static async Task<SqliteConnection> OpenInitializedDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");

        try
        {
            await connection.OpenAsync();
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
