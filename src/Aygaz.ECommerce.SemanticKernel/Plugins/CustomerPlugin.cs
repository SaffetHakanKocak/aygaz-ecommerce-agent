using System.ComponentModel;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Plugins;

public sealed class CustomerPlugin
{
    private readonly ICustomerService _customerService;
    private readonly int _maxNameSearchResults;

    public CustomerPlugin(ICustomerService customerService, int maxNameSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(customerService);

        if (maxNameSearchResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxNameSearchResults),
                "MaxNameSearchResults must be greater than zero.");
        }

        _customerService = customerService;
        _maxNameSearchResults = maxNameSearchResults;
    }

    [KernelFunction("get_customer_by_email")]
    [Description("Use only when the user provided an explicit email address. Do not use for name search or bulk listing.")]
    public async Task<CustomerAgentResult?> GetCustomerByEmailAsync(
        [Description("Exact email address.")] string email,
        CancellationToken cancellationToken = default)
    {
        CustomerDto? customer = await _customerService.GetCustomerByEmailAsync(
            email?.Trim() ?? string.Empty,
            cancellationToken);

        return customer is null ? null : ToResult(customer);
    }

    [KernelFunction("get_customer_by_id")]
        [Description("Use only when the user gave a specific numeric customer id or customer number. Do not use for email or name search.")]
    public async Task<CustomerAgentResult?> GetCustomerByIdAsync(
        [Description("Numeric customer id.")] int id,
        CancellationToken cancellationToken = default)
    {
        CustomerDto? customer = await _customerService.GetCustomerByIdAsync(id, cancellationToken);
        return customer is null ? null : ToResult(customer);
    }

    [KernelFunction("search_customers_by_name")]
    [Description("Use only when the user provided a person's first and/or last name. Do not use for bulk listing, generic customer list requests, or email lookup.")]
    public async Task<IReadOnlyList<CustomerAgentResult>> SearchCustomersByNameAsync(
        [Description("Person first name, last name, or full name.")] string query,
        CancellationToken cancellationToken = default)
    {
        string trimmedQuery = query?.Trim() ?? string.Empty;
        if (!LooksLikePersonNameQuery(trimmedQuery))
        {
            return [];
        }

        IReadOnlyList<CustomerDto> customers = await _customerService.SearchCustomersByNameAsync(
            trimmedQuery,
            cancellationToken);

        return customers
            .Take(_maxNameSearchResults)
            .Select(ToResult)
            .ToArray();
    }

    [KernelFunction("search_customers_by_city")]
    [Description("Use only for city-based customer lookup requests such as 'İstanbul'daki müşteriler'. Do not use for bulk-all customer listing.")]
    public async Task<IReadOnlyList<CustomerAgentResult>> SearchCustomersByCityAsync(
        [Description("City name such as İstanbul or Ankara.")] string city,
        CancellationToken cancellationToken = default)
    {
        string trimmedCity = city?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedCity))
        {
            return [];
        }

        IReadOnlyList<CustomerDto> customers = await _customerService.SearchCustomersByCityAsync(
            trimmedCity,
            cancellationToken);

        return customers
            .Take(5)
            .Select(ToResult)
            .ToArray();
    }

    private static bool LooksLikePersonNameQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        string[] tokens = query.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens.Any(IsPersonNameToken);
    }

    private static bool IsPersonNameToken(string token)
    {
        if (NonNameTokens.Contains(token))
        {
            return false;
        }

        return token.Length >= 2 && token.All(char.IsLetter);
    }

    private static readonly HashSet<string> NonNameTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "müşteri", "müşteriler", "müşteriyi", "müşterileri", "müşterilerin",
        "tüm", "hepsi", "bütün", "liste", "listesi", "listesini",
        "getir", "göster", "gösterin", "listele",
        "customer", "customers", "all", "list", "show", "get"
    };

    private static CustomerAgentResult ToResult(CustomerDto customer)
    {
        return new CustomerAgentResult(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.City,
            customer.Phone,
            customer.Address);
    }
}
