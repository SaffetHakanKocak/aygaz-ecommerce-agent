using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class ProductInventoryDataIntegrationTests
{
    [Fact]
    public async Task InitializeAsync_WhenCalledWithSeparateContexts_SeedsProductsAndInventoryExactlyOnce()
    {
        await using SqliteConnection connection = await OpenDatabaseAsync();
        DbContextOptions<ECommerceDbContext> options = CreateOptions(connection);

        await using (var firstContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(firstContext).InitializeAsync();
            Assert.Equal(12, await firstContext.Products.CountAsync());
            Assert.Equal(16, await firstContext.InventoryRecords.CountAsync());
        }

        await using (var secondContext = new ECommerceDbContext(options))
        {
            await new DatabaseInitializer(secondContext).InitializeAsync();
            Assert.Equal(12, await secondContext.Products.CountAsync());
            Assert.Equal(16, await secondContext.InventoryRecords.CountAsync());
            Assert.Equal(
                12,
                await secondContext.Products.Select(product => product.Sku)
                    .Distinct()
                    .CountAsync());
            Assert.Equal(
                16,
                await secondContext.InventoryRecords
                    .Select(record => new { record.ProductId, record.LocationCode })
                    .Distinct()
                    .CountAsync());

            Assert.Equal(12, await secondContext.Customers.CountAsync());
            Assert.Equal(24, await secondContext.CustomerOrders.CountAsync());
        }
    }

    [Fact]
    public async Task SeededProducts_MatchTheExactSyntheticCatalog()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));

        Product[] products = await context.Products
            .AsNoTracking()
            .OrderBy(product => product.Sku)
            .ToArrayAsync();

        var actual = products.Select(product => (
            product.Sku,
            product.Name,
            product.Category,
            product.UnitPrice,
            product.IsActive));
        var expected = new[]
        {
            ("AYG-DEMO-PRD-001", "Demo Product Alpha", "DemoCategoryA", 125.50m, true),
            ("AYG-DEMO-PRD-002", "Demo Product Beta", "DemoCategoryA", 249.90m, true),
            ("AYG-DEMO-PRD-003", "Demo Product Gamma", "DemoCategoryB", 79.25m, true),
            ("AYG-DEMO-PRD-004", "Demo Product Delta", "DemoCategoryB", 315.00m, true),
            ("AYG-DEMO-PRD-005", "Demo Product Epsilon", "DemoCategoryC", 410.40m, false),
            ("AYG-DEMO-PRD-006", "Demo Product Zeta", "DemoCategoryA", 58.75m, true),
            ("AYG-DEMO-PRD-007", "Demo Product Eta", "DemoCategoryB", 605.00m, true),
            ("AYG-DEMO-PRD-008", "Demo Product Theta", "DemoCategoryC", 199.99m, true),
            ("AYG-DEMO-PRD-009", "Demo Product Iota", "DemoCategoryA", 330.30m, true),
            ("AYG-DEMO-PRD-010", "Demo Product Kappa", "DemoCategoryA", 45.00m, true),
            ("AYG-DEMO-PRD-011", "Demo Product Lambda", "DemoCategoryC", 515.15m, true),
            ("AYG-DEMO-PRD-012", "Demo Product Mu", "DemoCategoryA", 88.80m, true)
        };

        Assert.Equal(expected, actual);
        Assert.Equal(
            new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            products[0].CreatedAt);
        Assert.Equal(
            new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc),
            products[^1].CreatedAt);
    }

    [Fact]
    public async Task SeededInventory_MatchesExactLocationsQuantitiesAndProductRelationship()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));

        var actual = await context.InventoryRecords
            .AsNoTracking()
            .OrderBy(record => record.Product.Sku)
            .ThenBy(record => record.LocationCode)
            .Select(record => new
            {
                record.Product.Sku,
                record.LocationCode,
                record.LocationName,
                record.QuantityAvailable,
                record.ReorderLevel
            })
            .ToArrayAsync();
        var expected = new[]
        {
            new { Sku = "AYG-DEMO-PRD-001", LocationCode = "DEMO-LOC-01", LocationName = "Demo Depo Bir", QuantityAvailable = 120, ReorderLevel = 20 },
            new { Sku = "AYG-DEMO-PRD-001", LocationCode = "DEMO-LOC-02", LocationName = "Demo Depo Iki", QuantityAvailable = 35, ReorderLevel = 10 },
            new { Sku = "AYG-DEMO-PRD-001", LocationCode = "DEMO-LOC-03", LocationName = "Demo Depo Uc", QuantityAvailable = 5, ReorderLevel = 5 },
            new { Sku = "AYG-DEMO-PRD-002", LocationCode = "DEMO-LOC-01", LocationName = "Demo Depo Bir", QuantityAvailable = 2, ReorderLevel = 5 },
            new { Sku = "AYG-DEMO-PRD-003", LocationCode = "DEMO-LOC-02", LocationName = "Demo Depo Iki", QuantityAvailable = 0, ReorderLevel = 5 },
            new { Sku = "AYG-DEMO-PRD-005", LocationCode = "DEMO-LOC-01", LocationName = "Demo Depo Bir", QuantityAvailable = 8, ReorderLevel = 4 },
            new { Sku = "AYG-DEMO-PRD-006", LocationCode = "DEMO-LOC-01", LocationName = "Demo Depo Bir", QuantityAvailable = 10, ReorderLevel = 5 },
            new { Sku = "AYG-DEMO-PRD-006", LocationCode = "DEMO-LOC-03", LocationName = "Demo Depo Uc", QuantityAvailable = 20, ReorderLevel = 5 },
            new { Sku = "AYG-DEMO-PRD-007", LocationCode = "DEMO-LOC-02", LocationName = "Demo Depo Iki", QuantityAvailable = 14, ReorderLevel = 7 },
            new { Sku = "AYG-DEMO-PRD-008", LocationCode = "DEMO-LOC-02", LocationName = "Demo Depo Iki", QuantityAvailable = 4, ReorderLevel = 5 },
            new { Sku = "AYG-DEMO-PRD-008", LocationCode = "DEMO-LOC-03", LocationName = "Demo Depo Uc", QuantityAvailable = 0, ReorderLevel = 3 },
            new { Sku = "AYG-DEMO-PRD-009", LocationCode = "DEMO-LOC-01", LocationName = "Demo Depo Bir", QuantityAvailable = 55, ReorderLevel = 15 },
            new { Sku = "AYG-DEMO-PRD-010", LocationCode = "DEMO-LOC-03", LocationName = "Demo Depo Uc", QuantityAvailable = 1, ReorderLevel = 3 },
            new { Sku = "AYG-DEMO-PRD-011", LocationCode = "DEMO-LOC-01", LocationName = "Demo Depo Bir", QuantityAvailable = 25, ReorderLevel = 10 },
            new { Sku = "AYG-DEMO-PRD-011", LocationCode = "DEMO-LOC-02", LocationName = "Demo Depo Iki", QuantityAvailable = 25, ReorderLevel = 10 },
            new { Sku = "AYG-DEMO-PRD-012", LocationCode = "DEMO-LOC-03", LocationName = "Demo Depo Uc", QuantityAvailable = 6, ReorderLevel = 5 }
        };

        Assert.Equal(expected, actual);
        Assert.DoesNotContain(actual, record => record.Sku == "AYG-DEMO-PRD-004");
    }

    [Fact]
    public async Task DatabaseModel_ConfiguresProductInventoryLengthsIndexesPrecisionAndRelationship()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));

        var product = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            context.Model.FindEntityType(typeof(Product)));
        Assert.Equal(
            Product.MaximumSkuLength,
            product.FindProperty(nameof(Product.Sku))?.GetMaxLength());
        Assert.Equal(
            Product.MaximumNameLength,
            product.FindProperty(nameof(Product.Name))?.GetMaxLength());
        Assert.Equal(
            Product.MaximumCategoryLength,
            product.FindProperty(nameof(Product.Category))?.GetMaxLength());
        Assert.Equal(18, product.FindProperty(nameof(Product.UnitPrice))?.GetPrecision());
        Assert.Equal(2, product.FindProperty(nameof(Product.UnitPrice))?.GetScale());
        Assert.Contains(
            product.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(Product.Sku)]));

        var inventory = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IEntityType>(
            context.Model.FindEntityType(typeof(InventoryRecord)));
        Assert.Equal(
            InventoryRecord.MaximumLocationCodeLength,
            inventory.FindProperty(nameof(InventoryRecord.LocationCode))?.GetMaxLength());
        Assert.Equal(
            InventoryRecord.MaximumLocationNameLength,
            inventory.FindProperty(nameof(InventoryRecord.LocationName))?.GetMaxLength());
        Assert.Contains(
            inventory.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(
                    [nameof(InventoryRecord.ProductId), nameof(InventoryRecord.LocationCode)]));
        var productForeignKey = Assert.Single(
            inventory.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(InventoryRecord.ProductId)]));
        Assert.True(productForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, productForeignKey.DeleteBehavior);
    }

    [Fact]
    public async Task Database_RejectsDuplicateSkuRegardlessOfAsciiCasing()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        context.Products.Add(CreateProduct("ayg-demo-prd-001"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_RejectsDuplicateProductLocationRegardlessOfAsciiCasing()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        int productId = await GetProductIdAsync(context, "AYG-DEMO-PRD-001");
        context.InventoryRecords.Add(CreateInventory(productId, "demo-loc-01"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_RejectsInventoryWithMissingProduct()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        context.InventoryRecords.Add(CreateInventory(int.MaxValue, "DEMO-LOC-X"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_RejectsNegativeProductPrice()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        Product product = CreateProduct("AYG-DEMO-PRD-NEGATIVE");
        product.UnitPrice = -0.01m;
        context.Products.Add(product);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task Database_RejectsNegativeInventoryValues(
        int quantityAvailable,
        int reorderLevel)
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        int productId = await GetProductIdAsync(context, "AYG-DEMO-PRD-004");
        InventoryRecord inventory = CreateInventory(productId, "DEMO-LOC-X");
        inventory.QuantityAvailable = quantityAvailable;
        inventory.ReorderLevel = reorderLevel;
        context.InventoryRecords.Add(inventory);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ProductService_GetByIdAndSku_ReturnsMinimumDtoAndHandlesInvalidInputs()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        var service = new ProductService(context);

        ProductDto bySku = Assert.IsType<ProductDto>(
            await service.GetProductBySkuAsync("  ayg-demo-prd-005  "));
        ProductDto byId = Assert.IsType<ProductDto>(
            await service.GetProductByIdAsync(bySku.Id));

        Assert.Equal(bySku, byId);
        Assert.Equal("Demo Product Epsilon", bySku.Name);
        Assert.False(bySku.IsActive);
        Assert.Null(await service.GetProductByIdAsync(0));
        Assert.Null(await service.GetProductByIdAsync(int.MaxValue));
        Assert.Null(await service.GetProductBySkuAsync("   "));
        Assert.Null(await service.GetProductBySkuAsync("AYG-DEMO-PRD-999"));
    }

    [Fact]
    public async Task ProductService_Search_IsCrossFieldDeterministicLimitedAndEscapesWildcards()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        var service = new ProductService(context);

        IReadOnlyList<ProductDto> category =
            await service.SearchProductsAsync("  democategorya  ", 5);
        IReadOnlyList<ProductDto> byName =
            await service.SearchProductsAsync("Product Alpha", 5);
        IReadOnlyList<ProductDto> bySku =
            await service.SearchProductsAsync("PRD-012", 5);

        Assert.Equal(
            new[]
            {
                "AYG-DEMO-PRD-001",
                "AYG-DEMO-PRD-002",
                "AYG-DEMO-PRD-006",
                "AYG-DEMO-PRD-009",
                "AYG-DEMO-PRD-010"
            },
            category.Select(product => product.Sku));
        Assert.Equal("AYG-DEMO-PRD-001", Assert.Single(byName).Sku);
        Assert.Equal("AYG-DEMO-PRD-012", Assert.Single(bySku).Sku);
        Assert.Empty(await service.SearchProductsAsync("%", 20));
        Assert.Empty(await service.SearchProductsAsync("_", 20));
        Assert.Empty(await service.SearchProductsAsync("   ", 5));
        Assert.Empty(await service.SearchProductsAsync("Demo", 0));
    }

    [Fact]
    public async Task InventoryService_IsOrderedLimitedAndDistinguishesZeroFromNoRows()
    {
        await using SqliteConnection connection = await OpenInitializedDatabaseAsync();
        await using var context = new ECommerceDbContext(CreateOptions(connection));
        var service = new InventoryService(context);
        int productOneId = await GetProductIdAsync(context, "AYG-DEMO-PRD-001");
        int productThreeId = await GetProductIdAsync(context, "AYG-DEMO-PRD-003");
        int productFourId = await GetProductIdAsync(context, "AYG-DEMO-PRD-004");

        IReadOnlyList<InventoryDto> limited =
            await service.GetProductInventoryAsync(productOneId, 2);

        Assert.Equal(
            new[] { "DEMO-LOC-01", "DEMO-LOC-02" },
            limited.Select(record => record.LocationCode));
        Assert.All(limited, record => Assert.Equal(productOneId, record.ProductId));
        Assert.Equal(160L, await service.GetTotalAvailableStockAsync(productOneId));
        Assert.Equal(0L, await service.GetTotalAvailableStockAsync(productThreeId));
        Assert.Null(await service.GetTotalAvailableStockAsync(productFourId));
        Assert.Empty(await service.GetProductInventoryAsync(productFourId, 5));
        Assert.Empty(await service.GetProductInventoryAsync(0, 5));
        Assert.Empty(await service.GetProductInventoryAsync(productOneId, 0));
        Assert.Null(await service.GetTotalAvailableStockAsync(0));
        Assert.Null(await service.GetTotalAvailableStockAsync(int.MaxValue));
    }

    private static Product CreateProduct(string sku)
    {
        return new Product
        {
            Sku = sku,
            Name = "Synthetic Constraint Product",
            Category = "DemoCategoryTest",
            UnitPrice = 1m,
            IsActive = true,
            CreatedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc)
        };
    }

    private static InventoryRecord CreateInventory(int productId, string locationCode)
    {
        return new InventoryRecord
        {
            ProductId = productId,
            LocationCode = locationCode,
            LocationName = "Synthetic Constraint Location",
            QuantityAvailable = 1,
            ReorderLevel = 1,
            UpdatedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc)
        };
    }

    private static Task<int> GetProductIdAsync(
        ECommerceDbContext context,
        string sku)
    {
        return context.Products
            .Where(product => product.Sku == sku)
            .Select(product => product.Id)
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
