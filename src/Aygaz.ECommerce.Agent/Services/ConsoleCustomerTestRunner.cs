using System.Data.Common;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class ConsoleCustomerTestRunner
{
    private readonly ICustomerService _customerService;

    public ConsoleCustomerTestRunner(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PrintMenu();

            string? selection;
            try
            {
                selection = await Console.In.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (selection is null || selection.Trim() == "0")
            {
                return;
            }

            try
            {
                await RunSelectionAsync(selection.Trim(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is DbException or InvalidOperationException)
            {
                Console.Error.WriteLine("Veritabanı işlemi tamamlanamadı.");
                Console.Error.WriteLine($"Teknik detay: {exception.Message}");
            }

            Console.WriteLine();
        }
    }

    private async Task RunSelectionAsync(string selection, CancellationToken cancellationToken)
    {
        switch (selection)
        {
            case "1":
                await ListAllCustomersAsync(cancellationToken);
                break;
            case "2":
                await FindCustomerByIdAsync(cancellationToken);
                break;
            case "3":
                await FindCustomerByEmailAsync(cancellationToken);
                break;
            case "4":
                await SearchCustomersByNameAsync(cancellationToken);
                break;
            default:
                Console.WriteLine("Geçersiz seçim. Lütfen 0-4 arasında bir değer girin.");
                break;
        }
    }

    private async Task ListAllCustomersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomerDto> customers =
            await _customerService.GetAllCustomersAsync(cancellationToken);

        if (customers.Count == 0)
        {
            PrintCustomerNotFound();
            return;
        }

        Console.WriteLine($"Toplam müşteri: {customers.Count}");
        foreach (CustomerDto customer in customers)
        {
            PrintCustomer(customer);
        }
    }

    private async Task FindCustomerByIdAsync(CancellationToken cancellationToken)
    {
        Console.Write("Müşteri ID: ");
        string? value = await Console.In.ReadLineAsync(cancellationToken);

        if (!int.TryParse(value, out int customerId) || customerId <= 0)
        {
            Console.WriteLine("Geçerli bir müşteri ID değeri girin.");
            return;
        }

        CustomerDto? customer =
            await _customerService.GetCustomerByIdAsync(customerId, cancellationToken);

        PrintCustomerOrNotFound(customer);
    }

    private async Task FindCustomerByEmailAsync(CancellationToken cancellationToken)
    {
        Console.Write("E-posta: ");
        string? email = await Console.In.ReadLineAsync(cancellationToken);

        CustomerDto? customer =
            await _customerService.GetCustomerByEmailAsync(email ?? string.Empty, cancellationToken);

        PrintCustomerOrNotFound(customer);
    }

    private async Task SearchCustomersByNameAsync(CancellationToken cancellationToken)
    {
        Console.Write("Ad / soyad: ");
        string? searchTerm = await Console.In.ReadLineAsync(cancellationToken);

        IReadOnlyList<CustomerDto> customers = await _customerService.SearchCustomersByNameAsync(
            searchTerm ?? string.Empty,
            cancellationToken);

        if (customers.Count == 0)
        {
            PrintCustomerNotFound();
            return;
        }

        Console.WriteLine($"Bulunan müşteri sayısı: {customers.Count}");
        foreach (CustomerDto customer in customers)
        {
            PrintCustomer(customer);
        }
    }

    private static void PrintMenu()
    {
        Console.WriteLine("-------------------------------");
        Console.WriteLine("Customer Database Test");
        Console.WriteLine("-------------------------------");
        Console.WriteLine("1 - Tüm müşterileri listele");
        Console.WriteLine("2 - ID ile müşteri bul");
        Console.WriteLine("3 - E-posta ile müşteri bul");
        Console.WriteLine("4 - Ad / soyad ile müşteri ara");
        Console.WriteLine("0 - Ana menü");
        Console.WriteLine();
        Console.WriteLine("Seçiminiz:");
        Console.Write("> ");
    }

    private static void PrintCustomerOrNotFound(CustomerDto? customer)
    {
        if (customer is null)
        {
            PrintCustomerNotFound();
            return;
        }

        PrintCustomer(customer);
    }

    private static void PrintCustomerNotFound()
    {
        Console.WriteLine("Müşteri bulunamadı.");
    }

    private static void PrintCustomer(CustomerDto customer)
    {
        Console.WriteLine($"[{customer.Id}] {customer.FirstName} {customer.LastName}");
        Console.WriteLine($"    E-posta: {customer.Email}");
        Console.WriteLine($"    Telefon: {customer.Phone ?? "-"}");
        Console.WriteLine($"    Şehir: {customer.City ?? "-"}");
        Console.WriteLine($"    Oluşturulma: {customer.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
    }
}
