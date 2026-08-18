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

            if (await dbContext.Customers.AsNoTracking().AnyAsync(cancellationToken))
            {
                return;
            }

            dbContext.Customers.AddRange(CreateSyntheticCustomers());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DatabaseInitializationException(
                "Müşteri veritabanı oluşturulamadı.",
                exception.Message,
                exception);
        }
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
}
