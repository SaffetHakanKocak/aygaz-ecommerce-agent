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

    private sealed record SyntheticOrderSeed(
        string CustomerEmail,
        string OrderNumber,
        DateTime OrderDate,
        OrderStatus Status,
        decimal TotalAmount);
}
