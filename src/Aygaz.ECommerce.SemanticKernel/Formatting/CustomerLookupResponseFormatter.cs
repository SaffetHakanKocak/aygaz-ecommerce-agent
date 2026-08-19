using Aygaz.ECommerce.Agent.Models.Agent;

namespace Aygaz.ECommerce.SemanticKernel.Formatting;

public static class CustomerLookupResponseFormatter
{
    public static bool TryFormatExactLookup(string functionName, object? value, out string text)
    {
        text = string.Empty;

        if (functionName is "get_customer_by_email" or "get_customer_by_id")
        {
            if (value is null)
            {
                text = "Bu bilgilerle kayıtlı müşteri bulunamadı.";
                return true;
            }

            if (value is CustomerAgentResult customer)
            {
                text = FormatCustomer(customer);
                return true;
            }

            return false;
        }

        if (functionName == "search_customers_by_name"
            && TryGetSingleCustomer(value, out CustomerAgentResult? match)
            && match is not null)
        {
            text = FormatCustomer(match);
            return true;
        }

        return false;
    }

    private static bool TryGetSingleCustomer(object? value, out CustomerAgentResult? customer)
    {
        customer = null;

        if (value is IEnumerable<CustomerAgentResult> results)
        {
            CustomerAgentResult[] matches = results.ToArray();
            if (matches.Length != 1)
            {
                return false;
            }

            customer = matches[0];
            return true;
        }

        return false;
    }

    public static string FormatCustomer(CustomerAgentResult customer)
    {
        string city = string.IsNullOrWhiteSpace(customer.City) ? "şehir yok" : customer.City;
        return $"Müşteri: {customer.FirstName} {customer.LastName} (ID: {customer.Id}, {city}).";
    }
}
