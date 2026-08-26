using System.Globalization;
using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class CustomerService(IECommerceDataAccess dataAccess) : ICustomerService
{
    private const CompareOptions NameSearchOptions =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private static readonly CompareInfo TurkishCompareInfo =
        CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public Task<CustomerDto?> GetCustomerByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Task.FromResult<CustomerDto?>(null);
        }

        return dataAccess.GetCustomerByIdAsync(id, cancellationToken);
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

        return dataAccess.GetCustomerByEmailAsync(normalizedEmail, cancellationToken);
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

        IReadOnlyList<CustomerDto> customers = await dataAccess
            .GetAllCustomersAsync(cancellationToken);

        return customers
            .Where(customer => MatchesAllTokens(customer, searchTokens, cancellationToken))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersByCityAsync(
        string city,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return Array.Empty<CustomerDto>();
        }

        string normalizedCity = city.Trim();
        return await dataAccess.SearchCustomersByCityAsync(normalizedCity, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(
        CancellationToken cancellationToken = default)
    {
        return await dataAccess.GetAllCustomersAsync(cancellationToken);
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
