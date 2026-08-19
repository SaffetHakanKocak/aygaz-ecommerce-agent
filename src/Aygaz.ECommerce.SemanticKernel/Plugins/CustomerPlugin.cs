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
    [Description("Looks up a single customer by exact email address. Call only when the user provided a specific email address. Do not call for bulk customer listing.")]
    public async Task<CustomerAgentResult?> GetCustomerByEmailAsync(
        [Description("The customer's exact email address.")] string email,
        CancellationToken cancellationToken = default)
    {
        CustomerDto? customer = await _customerService.GetCustomerByEmailAsync(
            email?.Trim() ?? string.Empty,
            cancellationToken);

        return customer is null ? null : ToResult(customer);
    }

    [KernelFunction("get_customer_by_id")]
    [Description("Looks up a single customer by positive customer id. Call only when the user provided a specific numeric customer id. Do not call for bulk customer listing.")]
    public async Task<CustomerAgentResult?> GetCustomerByIdAsync(
        [Description("The customer's positive numeric identifier.")] int id,
        CancellationToken cancellationToken = default)
    {
        CustomerDto? customer = await _customerService.GetCustomerByIdAsync(id, cancellationToken);
        return customer is null ? null : ToResult(customer);
    }

    [KernelFunction("search_customers_by_name")]
    [Description("Searches customers by a specific person's first name, last name, or full name. Call only when the user explicitly provided a person name. Never call this when the user asks to list, fetch, or show all customers without naming a specific person.")]
    public async Task<IReadOnlyList<CustomerAgentResult>> SearchCustomersByNameAsync(
        [Description("The customer's first name, last name, or full name. Must be a person name, not a generic listing request.")] string query,
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
            customer.City);
    }
}
