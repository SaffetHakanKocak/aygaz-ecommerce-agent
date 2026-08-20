using System.Globalization;
using System.Text;
using Aygaz.ECommerce.Agent.Models.Agent;

namespace Aygaz.ECommerce.SemanticKernel.Formatting;

public enum CustomerLookupDetail
{
    Summary,
    Phone,
    Address,
    Email,
    Full
}

public static class CustomerLookupResponseFormatter
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool TryFormatExactLookup(string functionName, object? value, out string text)
    {
        return TryFormatExactLookup(functionName, value, userMessage: null, out text);
    }

    public static bool TryFormatExactLookup(
        string functionName,
        object? value,
        string? userMessage,
        out string text)
    {
        text = string.Empty;
        CustomerLookupDetail detail = ResolveDetail(userMessage);

        if (functionName is "get_customer_by_email" or "get_customer_by_id")
        {
            if (value is null)
            {
                text = "Bu bilgilerle kayıtlı müşteri bulunamadı.";
                return true;
            }

            if (value is CustomerAgentResult customer)
            {
                text = FormatCustomer(customer, detail);
                return true;
            }

            return false;
        }

        if (functionName == "search_customers_by_name"
            && TryGetSingleCustomer(value, out CustomerAgentResult? match)
            && match is not null)
        {
            text = FormatCustomer(match, detail);
            return true;
        }

        return false;
    }

    public static CustomerLookupDetail ResolveDetail(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return CustomerLookupDetail.Summary;
        }

        string normalized = userMessage.ToLower(Turkish);
        bool phone = ContainsPhoneIntent(normalized);
        bool address = ContainsAddressIntent(normalized);
        bool email = ContainsEmailIntent(normalized);

        if (phone && !address && !email)
        {
            return CustomerLookupDetail.Phone;
        }

        if (address && !phone && !email)
        {
            return CustomerLookupDetail.Address;
        }

        if (email && !phone && !address)
        {
            return CustomerLookupDetail.Email;
        }

        if (ContainsFullInfoIntent(normalized))
        {
            return CustomerLookupDetail.Full;
        }

        return CustomerLookupDetail.Summary;
    }

    private static bool ContainsPhoneIntent(string normalized)
    {
        return normalized.Contains("telefon", StringComparison.Ordinal)
            || normalized.Contains("phone", StringComparison.Ordinal);
    }

    private static bool ContainsAddressIntent(string normalized)
    {
        return normalized.Contains("adres", StringComparison.Ordinal)
            || normalized.Contains("address", StringComparison.Ordinal);
    }

    private static bool ContainsEmailIntent(string normalized)
    {
        return normalized.Contains("e-posta", StringComparison.Ordinal)
            || normalized.Contains("eposta", StringComparison.Ordinal)
            || normalized.Contains("email", StringComparison.Ordinal);
    }

    private static bool ContainsFullInfoIntent(string normalized)
    {
        return normalized.Contains("bilgilerini", StringComparison.Ordinal)
            || normalized.Contains("bilgileri getir", StringComparison.Ordinal)
            || normalized.Contains("bilgileri göster", StringComparison.Ordinal)
            || normalized.Contains("bilgileri goster", StringComparison.Ordinal)
            || normalized.Contains("tüm bilgi", StringComparison.Ordinal)
            || normalized.Contains("tum bilgi", StringComparison.Ordinal)
            || normalized.Contains("müşteri bilgiler", StringComparison.Ordinal)
            || normalized.Contains("musteri bilgiler", StringComparison.Ordinal);
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
        return FormatCustomer(customer, CustomerLookupDetail.Summary);
    }

    public static string FormatCustomer(CustomerAgentResult customer, CustomerLookupDetail detail)
    {
        string name = $"{customer.FirstName} {customer.LastName}";
        return detail switch
        {
            CustomerLookupDetail.Phone =>
                $"{name}'ın telefon numarası: {ValueOrUnknown(customer.PhoneNumber)}",
            CustomerLookupDetail.Address =>
                $"{name}'ın adresi: {ValueOrUnknown(customer.Address)}"
                + (string.IsNullOrWhiteSpace(customer.City) ? string.Empty : $", {customer.City}"),
            CustomerLookupDetail.Email =>
                $"{name}'ın e-postası: {ValueOrUnknown(customer.Email)}",
            CustomerLookupDetail.Full => FormatFullSummary(customer),
            _ => FormatSummary(customer)
        };
    }

    private static string FormatSummary(CustomerAgentResult customer)
    {
        string city = string.IsNullOrWhiteSpace(customer.City) ? "şehir yok" : customer.City;
        return $"Müşteri: {customer.FirstName} {customer.LastName} (ID: {customer.Id}, {city}).";
    }

    private static string FormatFullSummary(CustomerAgentResult customer)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Müşteri Bilgileri");
        builder.AppendLine($"Müşteri No: {customer.Id}");
        builder.AppendLine($"Ad Soyad: {customer.FirstName} {customer.LastName}");
        builder.AppendLine($"E-posta: {ValueOrUnknown(customer.Email)}");
        builder.AppendLine($"Telefon: {ValueOrUnknown(customer.PhoneNumber)}");
        builder.AppendLine($"Adres: {ValueOrUnknown(customer.Address)}");
        builder.Append($"Şehir: {ValueOrUnknown(customer.City)}");
        return builder.ToString();
    }

    private static string ValueOrUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "kayıtlı değil" : value;
    }
}
