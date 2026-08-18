using Aygaz.ECommerce.Agent.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Data;

public sealed class DatabaseInitializer(ECommerceDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            await SeedCustomersAsync(cancellationToken);
            await SeedOrdersAsync(cancellationToken);
            await SeedProductsAsync(cancellationToken);
            await SeedOrderItemsAsync(cancellationToken);
            await SeedInventoryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DatabaseInitializationException(
                "E-ticaret veritabanı oluşturulamadı.",
                exception.Message,
                exception);
        }
    }

    private async Task SeedCustomersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Customer> syntheticCustomers = CreateSyntheticCustomers();
        string[] existingEmails = await dbContext.Customers
            .AsNoTracking()
            .Select(customer => customer.Email)
            .ToArrayAsync(cancellationToken);
        var existingEmailSet = new HashSet<string>(
            existingEmails,
            StringComparer.OrdinalIgnoreCase);

        Customer[] missingCustomers = syntheticCustomers
            .Where(customer => !existingEmailSet.Contains(customer.Email))
            .ToArray();

        if (missingCustomers.Length == 0)
        {
            return;
        }

        dbContext.Customers.AddRange(missingCustomers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedOrdersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SyntheticOrderSeed> syntheticOrders = CreateSyntheticOrders();
        string[] existingOrderNumbers = await dbContext.CustomerOrders
            .AsNoTracking()
            .Select(order => order.OrderNumber)
            .ToArrayAsync(cancellationToken);
        var existingOrderNumberSet = new HashSet<string>(
            existingOrderNumbers,
            StringComparer.OrdinalIgnoreCase);

        SyntheticOrderSeed[] missingOrderSeeds = syntheticOrders
            .Where(seed => !existingOrderNumberSet.Contains(seed.OrderNumber))
            .ToArray();

        if (missingOrderSeeds.Length == 0)
        {
            return;
        }

        List<Customer> customers = await dbContext.Customers
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        Dictionary<string, Customer> customersByEmail = customers.ToDictionary(
            customer => customer.Email,
            StringComparer.OrdinalIgnoreCase);

        var missingOrders = new List<CustomerOrder>(missingOrderSeeds.Length);

        foreach (SyntheticOrderSeed seed in missingOrderSeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!customersByEmail.TryGetValue(seed.CustomerEmail, out Customer? customer))
            {
                throw new InvalidOperationException(
                    $"Sentetik sipariş müşterisi bulunamadı: {seed.CustomerEmail}");
            }

            missingOrders.Add(new CustomerOrder
            {
                OrderNumber = seed.OrderNumber,
                CustomerId = customer.Id,
                OrderDate = seed.OrderDate,
                Status = seed.Status,
                TotalAmount = seed.TotalAmount
            });
        }

        dbContext.CustomerOrders.AddRange(missingOrders);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedProductsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Product> syntheticProducts = CreateSyntheticProducts();
        string[] existingSkus = await dbContext.Products
            .AsNoTracking()
            .Select(product => product.Sku)
            .ToArrayAsync(cancellationToken);
        var existingSkuSet = new HashSet<string>(
            existingSkus,
            StringComparer.OrdinalIgnoreCase);

        Product[] missingProducts = syntheticProducts
            .Where(product => !existingSkuSet.Contains(product.Sku))
            .ToArray();

        if (missingProducts.Length == 0)
        {
            return;
        }

        dbContext.Products.AddRange(missingProducts);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedInventoryAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SyntheticInventorySeed> syntheticInventory =
            CreateSyntheticInventory();

        var existingInventoryKeys = new HashSet<string>(
            (await dbContext.InventoryRecords
                .AsNoTracking()
                .Select(record => new
                {
                    record.Product.Sku,
                    record.LocationCode
                })
                .ToListAsync(cancellationToken))
            .Select(record => CreateInventoryKey(
                record.Sku,
                record.LocationCode)),
            StringComparer.OrdinalIgnoreCase);

        SyntheticInventorySeed[] missingInventorySeeds = syntheticInventory
            .Where(seed => !existingInventoryKeys.Contains(
                CreateInventoryKey(seed.ProductSku, seed.LocationCode)))
            .ToArray();

        if (missingInventorySeeds.Length == 0)
        {
            return;
        }

        Dictionary<string, int> productIdsBySku = (await dbContext.Products
                .AsNoTracking()
                .Select(product => new
                {
                    product.Sku,
                    product.Id
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                product => product.Sku,
                product => product.Id,
                StringComparer.OrdinalIgnoreCase);

        var missingInventory = new List<InventoryRecord>(
            missingInventorySeeds.Length);

        foreach (SyntheticInventorySeed seed in missingInventorySeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!productIdsBySku.TryGetValue(seed.ProductSku, out int productId))
            {
                throw new InvalidOperationException(
                    $"Sentetik inventory ürünü bulunamadı: {seed.ProductSku}");
            }

            missingInventory.Add(new InventoryRecord
            {
                ProductId = productId,
                LocationCode = seed.LocationCode,
                LocationName = seed.LocationName,
                QuantityAvailable = seed.QuantityAvailable,
                ReorderLevel = seed.ReorderLevel,
                UpdatedAt = seed.UpdatedAt
            });
        }

        dbContext.InventoryRecords.AddRange(missingInventory);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedOrderItemsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SyntheticOrderItemSeed> syntheticOrderItems =
            CreateSyntheticOrderItems();

        var existingOrderItemKeys = new HashSet<string>(
            (await dbContext.OrderItems
                .AsNoTracking()
                .Select(item => new
                {
                    item.CustomerOrder.OrderNumber,
                    item.Product.Sku
                })
                .ToListAsync(cancellationToken))
            .Select(item => CreateOrderItemKey(
                item.OrderNumber,
                item.Sku)),
            StringComparer.OrdinalIgnoreCase);

        SyntheticOrderItemSeed[] missingOrderItemSeeds = syntheticOrderItems
            .Where(seed => !existingOrderItemKeys.Contains(
                CreateOrderItemKey(seed.OrderNumber, seed.ProductSku)))
            .ToArray();

        if (missingOrderItemSeeds.Length == 0)
        {
            return;
        }

        Dictionary<string, int> orderIdsByNumber = (await dbContext.CustomerOrders
                .AsNoTracking()
                .Select(order => new
                {
                    order.OrderNumber,
                    order.Id
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                order => order.OrderNumber,
                order => order.Id,
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, int> productIdsBySku = (await dbContext.Products
                .AsNoTracking()
                .Select(product => new
                {
                    product.Sku,
                    product.Id
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                product => product.Sku,
                product => product.Id,
                StringComparer.OrdinalIgnoreCase);

        var missingOrderItems = new List<OrderItem>(missingOrderItemSeeds.Length);

        foreach (SyntheticOrderItemSeed seed in missingOrderItemSeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!orderIdsByNumber.TryGetValue(seed.OrderNumber, out int orderId))
            {
                throw new InvalidOperationException(
                    $"Sentetik sipariş kalemi siparişi bulunamadı: {seed.OrderNumber}");
            }

            if (!productIdsBySku.TryGetValue(seed.ProductSku, out int productId))
            {
                throw new InvalidOperationException(
                    $"Sentetik sipariş kalemi ürünü bulunamadı: {seed.ProductSku}");
            }

            missingOrderItems.Add(new OrderItem
            {
                CustomerOrderId = orderId,
                ProductId = productId,
                Quantity = seed.Quantity,
                UnitPrice = seed.UnitPrice
            });
        }

        dbContext.OrderItems.AddRange(missingOrderItems);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<Customer> CreateSyntheticCustomers()
    {
        DateTime seedDate = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

        return
        [
            new Customer
            {
                FirstName = "Ahmet",
                LastName = "Yılmaz",
                Email = "ahmet.yilmaz@example.com",
                Phone = "000-000-0001 (TEST)",
                City = "İstanbul",
                CreatedAt = seedDate
            },
            new Customer
            {
                FirstName = "Mehmet",
                LastName = "Kaya",
                Email = "mehmet.kaya@example.com",
                Phone = null,
                City = "Ankara",
                CreatedAt = seedDate.AddDays(1)
            },
            new Customer
            {
                FirstName = "Ayşe",
                LastName = "Demir",
                Email = "ayse.demir@example.com",
                Phone = "000-000-0003 (TEST)",
                City = "İzmir",
                CreatedAt = seedDate.AddDays(2)
            },
            new Customer
            {
                FirstName = "Zeynep",
                LastName = "Aydın",
                Email = "zeynep.aydin@example.com",
                Phone = null,
                City = "Kocaeli",
                CreatedAt = seedDate.AddDays(3)
            },
            new Customer
            {
                FirstName = "Emre",
                LastName = "Şahin",
                Email = "emre.sahin@example.com",
                Phone = "000-000-0005 (TEST)",
                City = "Konya",
                CreatedAt = seedDate.AddDays(4)
            },
            new Customer
            {
                FirstName = "Elif",
                LastName = "Çelik",
                Email = "elif.celik@example.com",
                Phone = null,
                City = "Bursa",
                CreatedAt = seedDate.AddDays(5)
            },
            new Customer
            {
                FirstName = "Can",
                LastName = "Koç",
                Email = "can.koc@example.com",
                Phone = "000-000-0007 (TEST)",
                City = "Antalya",
                CreatedAt = seedDate.AddDays(6)
            },
            new Customer
            {
                FirstName = "Deniz",
                LastName = "Arslan",
                Email = "deniz.arslan@example.com",
                Phone = null,
                City = "Adana",
                CreatedAt = seedDate.AddDays(7)
            },
            new Customer
            {
                FirstName = "Selin",
                LastName = "Güneş",
                Email = "selin.gunes@example.com",
                Phone = "000-000-0009 (TEST)",
                City = "Eskişehir",
                CreatedAt = seedDate.AddDays(8)
            },
            new Customer
            {
                FirstName = "Murat",
                LastName = "Aksoy",
                Email = "murat.aksoy@example.com",
                Phone = null,
                City = "Gaziantep",
                CreatedAt = seedDate.AddDays(9)
            },
            new Customer
            {
                FirstName = "Ece",
                LastName = "Öztürk",
                Email = "ece.ozturk@example.com",
                Phone = "000-000-0011 (TEST)",
                City = "Samsun",
                CreatedAt = seedDate.AddDays(10)
            },
            new Customer
            {
                FirstName = "Burak",
                LastName = "Yıldız",
                Email = "burak.yildiz@example.com",
                Phone = null,
                City = "Kayseri",
                CreatedAt = seedDate.AddDays(11)
            }
        ];
    }

    private static IReadOnlyList<SyntheticOrderSeed> CreateSyntheticOrders()
    {
        DateTime seedDate = new(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        return
        [
            new(
                "ahmet.yilmaz@example.com",
                "AYG-DEMO-1001",
                seedDate,
                OrderStatus.Delivered,
                1250.00m),
            new(
                "ahmet.yilmaz@example.com",
                "AYG-DEMO-1002",
                seedDate.AddDays(7),
                OrderStatus.Cancelled,
                340.50m),
            new(
                "ahmet.yilmaz@example.com",
                "AYG-DEMO-1003",
                seedDate.AddDays(14),
                OrderStatus.Shipped,
                780.25m),
            new(
                "ahmet.yilmaz@example.com",
                "AYG-DEMO-1004",
                seedDate.AddDays(21),
                OrderStatus.Preparing,
                910.75m),
            new(
                "mehmet.kaya@example.com",
                "AYG-DEMO-1005",
                seedDate.AddDays(1),
                OrderStatus.Pending,
                215.00m),
            new(
                "mehmet.kaya@example.com",
                "AYG-DEMO-1006",
                seedDate.AddDays(24),
                OrderStatus.Delivered,
                640.90m),
            new(
                "ayse.demir@example.com",
                "AYG-DEMO-1007",
                seedDate.AddDays(2),
                OrderStatus.Preparing,
                475.40m),
            new(
                "ayse.demir@example.com",
                "AYG-DEMO-1008",
                seedDate.AddDays(25),
                OrderStatus.Shipped,
                1120.00m),
            new(
                "zeynep.aydin@example.com",
                "AYG-DEMO-1009",
                seedDate.AddDays(3),
                OrderStatus.Cancelled,
                180.75m),
            new(
                "zeynep.aydin@example.com",
                "AYG-DEMO-1010",
                seedDate.AddDays(26),
                OrderStatus.Pending,
                825.30m),
            new(
                "emre.sahin@example.com",
                "AYG-DEMO-1011",
                seedDate.AddDays(4),
                OrderStatus.Delivered,
                990.00m),
            new(
                "emre.sahin@example.com",
                "AYG-DEMO-1012",
                seedDate.AddDays(27),
                OrderStatus.Preparing,
                305.60m),
            new(
                "elif.celik@example.com",
                "AYG-DEMO-1013",
                seedDate.AddDays(5),
                OrderStatus.Shipped,
                560.45m),
            new(
                "elif.celik@example.com",
                "AYG-DEMO-1014",
                seedDate.AddDays(28),
                OrderStatus.Delivered,
                1450.00m),
            new(
                "can.koc@example.com",
                "AYG-DEMO-1015",
                seedDate.AddDays(6),
                OrderStatus.Pending,
                275.10m),
            new(
                "can.koc@example.com",
                "AYG-DEMO-1016",
                seedDate.AddDays(29),
                OrderStatus.Cancelled,
                710.80m),
            new(
                "deniz.arslan@example.com",
                "AYG-DEMO-1017",
                seedDate.AddDays(7),
                OrderStatus.Preparing,
                430.00m),
            new(
                "deniz.arslan@example.com",
                "AYG-DEMO-1018",
                seedDate.AddDays(30),
                OrderStatus.Shipped,
                875.55m),
            new(
                "selin.gunes@example.com",
                "AYG-DEMO-1019",
                seedDate.AddDays(8),
                OrderStatus.Delivered,
                1320.20m),
            new(
                "selin.gunes@example.com",
                "AYG-DEMO-1020",
                seedDate.AddDays(31),
                OrderStatus.Pending,
                195.90m),
            new(
                "murat.aksoy@example.com",
                "AYG-DEMO-1021",
                seedDate.AddDays(9),
                OrderStatus.Cancelled,
                680.00m),
            new(
                "murat.aksoy@example.com",
                "AYG-DEMO-1022",
                seedDate.AddDays(32),
                OrderStatus.Delivered,
                1045.35m),
            new(
                "ece.ozturk@example.com",
                "AYG-DEMO-1023",
                seedDate.AddDays(10),
                OrderStatus.Shipped,
                520.00m),
            new(
                "ece.ozturk@example.com",
                "AYG-DEMO-1024",
                seedDate.AddDays(33),
                OrderStatus.Preparing,
                760.65m)
        ];
    }

    private static IReadOnlyList<Product> CreateSyntheticProducts()
    {
        DateTime seedDate = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        return
        [
            CreateProduct("001", "Alpha", "DemoCategoryA", 125.50m, true, seedDate),
            CreateProduct("002", "Beta", "DemoCategoryA", 249.90m, true, seedDate.AddDays(1)),
            CreateProduct("003", "Gamma", "DemoCategoryB", 79.25m, true, seedDate.AddDays(2)),
            CreateProduct("004", "Delta", "DemoCategoryB", 315.00m, true, seedDate.AddDays(3)),
            CreateProduct("005", "Epsilon", "DemoCategoryC", 410.40m, false, seedDate.AddDays(4)),
            CreateProduct("006", "Zeta", "DemoCategoryA", 58.75m, true, seedDate.AddDays(5)),
            CreateProduct("007", "Eta", "DemoCategoryB", 605.00m, true, seedDate.AddDays(6)),
            CreateProduct("008", "Theta", "DemoCategoryC", 199.99m, true, seedDate.AddDays(7)),
            CreateProduct("009", "Iota", "DemoCategoryA", 330.30m, true, seedDate.AddDays(8)),
            CreateProduct("010", "Kappa", "DemoCategoryA", 45.00m, true, seedDate.AddDays(9)),
            CreateProduct("011", "Lambda", "DemoCategoryC", 515.15m, true, seedDate.AddDays(10)),
            CreateProduct("012", "Mu", "DemoCategoryA", 88.80m, true, seedDate.AddDays(11))
        ];
    }

    private static Product CreateProduct(
        string skuSuffix,
        string nameSuffix,
        string category,
        decimal unitPrice,
        bool isActive,
        DateTime createdAt)
    {
        return new Product
        {
            Sku = $"AYG-DEMO-PRD-{skuSuffix}",
            Name = $"Demo Product {nameSuffix}",
            Category = category,
            UnitPrice = unitPrice,
            IsActive = isActive,
            CreatedAt = createdAt
        };
    }

    private static IReadOnlyList<SyntheticInventorySeed> CreateSyntheticInventory()
    {
        DateTime seedDate = new(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

        return
        [
            CreateInventory("001", "01", "Demo Depo Bir", 120, 20, seedDate),
            CreateInventory("001", "02", "Demo Depo Iki", 35, 10, seedDate.AddHours(1)),
            CreateInventory("001", "03", "Demo Depo Uc", 5, 5, seedDate.AddHours(2)),
            CreateInventory("002", "01", "Demo Depo Bir", 2, 5, seedDate.AddHours(3)),
            CreateInventory("003", "02", "Demo Depo Iki", 0, 5, seedDate.AddHours(4)),
            CreateInventory("005", "01", "Demo Depo Bir", 8, 4, seedDate.AddHours(5)),
            CreateInventory("006", "01", "Demo Depo Bir", 10, 5, seedDate.AddHours(6)),
            CreateInventory("006", "03", "Demo Depo Uc", 20, 5, seedDate.AddHours(7)),
            CreateInventory("007", "02", "Demo Depo Iki", 14, 7, seedDate.AddHours(8)),
            CreateInventory("008", "02", "Demo Depo Iki", 4, 5, seedDate.AddHours(9)),
            CreateInventory("008", "03", "Demo Depo Uc", 0, 3, seedDate.AddHours(10)),
            CreateInventory("009", "01", "Demo Depo Bir", 55, 15, seedDate.AddHours(11)),
            CreateInventory("010", "03", "Demo Depo Uc", 1, 3, seedDate.AddHours(12)),
            CreateInventory("011", "01", "Demo Depo Bir", 25, 10, seedDate.AddHours(13)),
            CreateInventory("011", "02", "Demo Depo Iki", 25, 10, seedDate.AddHours(14)),
            CreateInventory("012", "03", "Demo Depo Uc", 6, 5, seedDate.AddHours(15))
        ];
    }

    private static IReadOnlyList<SyntheticOrderItemSeed> CreateSyntheticOrderItems()
    {
        IReadOnlyList<SyntheticOrderSeed> syntheticOrders = CreateSyntheticOrders();
        var orderItems = new List<SyntheticOrderItemSeed>(syntheticOrders.Count * 2);

        for (int index = 0; index < syntheticOrders.Count; index++)
        {
            SyntheticOrderSeed order = syntheticOrders[index];
            int secondaryProductNumber = (index % 11) + 2;

            orderItems.Add(new SyntheticOrderItemSeed(
                order.OrderNumber,
                "AYG-DEMO-PRD-001",
                3,
                10.00m));
            orderItems.Add(new SyntheticOrderItemSeed(
                order.OrderNumber,
                $"AYG-DEMO-PRD-{secondaryProductNumber:000}",
                1,
                order.TotalAmount - 30.00m));
        }

        return orderItems;
    }

    private static SyntheticInventorySeed CreateInventory(
        string skuSuffix,
        string locationSuffix,
        string locationName,
        int quantityAvailable,
        int reorderLevel,
        DateTime updatedAt)
    {
        return new SyntheticInventorySeed(
            $"AYG-DEMO-PRD-{skuSuffix}",
            $"DEMO-LOC-{locationSuffix}",
            locationName,
            quantityAvailable,
            reorderLevel,
            updatedAt);
    }

    private static string CreateInventoryKey(
        string productSku,
        string locationCode)
    {
        return $"{productSku}\u001F{locationCode}";
    }

    private static string CreateOrderItemKey(
        string orderNumber,
        string productSku)
    {
        return $"{orderNumber}\u001F{productSku}";
    }

    private sealed record SyntheticOrderSeed(
        string CustomerEmail,
        string OrderNumber,
        DateTime OrderDate,
        OrderStatus Status,
        decimal TotalAmount);

    private sealed record SyntheticInventorySeed(
        string ProductSku,
        string LocationCode,
        string LocationName,
        int QuantityAvailable,
        int ReorderLevel,
        DateTime UpdatedAt);

    private sealed record SyntheticOrderItemSeed(
        string OrderNumber,
        string ProductSku,
        int Quantity,
        decimal UnitPrice);
}
