using System.Globalization;
using System.Linq.Expressions;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class CustomerService(ECommerceDbContext dbContext) : ICustomerService
{
    private const CompareOptions NameSearchOptions =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private static readonly CompareInfo TurkishCompareInfo =
        CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    private static readonly Expression<Func<Customer, CustomerDto>> ToDto = customer =>
        new CustomerDto(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.CreatedAt);

    public Task<CustomerDto?> GetCustomerByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Task.FromResult<CustomerDto?>(null);
        }

        return dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CustomerDto?> GetCustomerByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult<CustomerDto?>(null);
        }

        string normalizedEmail = email.Trim();

        return dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.Email == normalizedEmail)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<CustomerDto>();
        }

        string[] searchTokens = searchTerm.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (searchTokens.Length == 0)
        {
            return Array.Empty<CustomerDto>();
        }

        List<CustomerDto> customers = await dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Id)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return customers
            .Where(customer => MatchesAllTokens(customer, searchTokens, cancellationToken))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Id)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    private static bool MatchesAllTokens(
        CustomerDto customer,
        IReadOnlyList<string> searchTokens,
        CancellationToken cancellationToken)
    {
        string fullName = $"{customer.FirstName} {customer.LastName}";

        foreach (string searchToken in searchTokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TurkishCompareInfo.IndexOf(fullName, searchToken, NameSearchOptions) < 0)
            {
                return false;
            }
        }

        return true;
    }
}
