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
    City,
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

        if (functionName == "search_customers_by_city"
            && value is IEnumerable<CustomerAgentResult> cityMatches)
        {
            text = FormatCityMatches(cityMatches, userMessage);
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
        bool city = ContainsCityIntent(normalized);

        if (phone && !address && !email && !city)
        {
            return CustomerLookupDetail.Phone;
        }

        if (address && !phone && !email && !city)
        {
            return CustomerLookupDetail.Address;
        }

        if (email && !phone && !address && !city)
        {
            return CustomerLookupDetail.Email;
        }

        if (city && !phone && !address && !email)
        {
            return CustomerLookupDetail.City;
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
            || normalized.Contains("adres bilgisi", StringComparison.Ordinal)
            || normalized.Contains("nerede yaşıyor", StringComparison.Ordinal)
            || normalized.Contains("nerede yasiyor", StringComparison.Ordinal)
            || normalized.Contains("nerede yaşıyordu", StringComparison.Ordinal)
            || normalized.Contains("nerede yasiyordu", StringComparison.Ordinal)
            || normalized.Contains("nerede oturuyor", StringComparison.Ordinal)
            || normalized.Contains("ikamet adresi", StringComparison.Ordinal)
            || normalized.Contains("ikametgah", StringComparison.Ordinal)
            || normalized.Contains("address", StringComparison.Ordinal);
    }

    private static bool ContainsEmailIntent(string normalized)
    {
        return normalized.Contains("e-posta", StringComparison.Ordinal)
            || normalized.Contains("eposta", StringComparison.Ordinal)
            || normalized.Contains("email", StringComparison.Ordinal);
    }

    private static bool ContainsCityIntent(string normalized)
    {
        return normalized.Contains("hangi şehir", StringComparison.Ordinal)
            || normalized.Contains("hangi sehir", StringComparison.Ordinal)
            || normalized.Contains("şehirdeydi", StringComparison.Ordinal)
            || normalized.Contains("sehirdeydi", StringComparison.Ordinal)
            || normalized.Contains("şehri neydi", StringComparison.Ordinal)
            || normalized.Contains("sehri neydi", StringComparison.Ordinal);
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
            CustomerLookupDetail.City =>
                $"{name}'ın şehri: {ValueOrUnknown(customer.City)}",
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

    private static string FormatCityMatches(IEnumerable<CustomerAgentResult> matches, string? userMessage)
    {
        CustomerAgentResult[] list = matches.Take(5).ToArray();
        string city = ExtractCity(userMessage) ?? "Bu şehir";
        if (list.Length == 0)
        {
            return $"{city} için müşteri bulunamadı.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{city}'da bulunan müşteriler:");
        foreach (CustomerAgentResult customer in list)
        {
            builder.AppendLine($"- {customer.FirstName} {customer.LastName} — Müşteri No: {customer.Id}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string? ExtractCity(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string normalized = message.Trim();
        string[] separators = ["'da", "'de", "da ", "de ", "daki", "deki", "için", "icin"];
        foreach (string separator in separators)
        {
            int index = normalized.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (index <= 0)
            {
                continue;
            }

            string candidate = normalized[..index].Trim();
            string[] tokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            string city = tokens[^1].Trim('\'', '"', '.', ',', '?', '!');
            if (city.Length >= 2 && city.All(char.IsLetter))
            {
                return city;
            }
        }

        return null;
    }
}
