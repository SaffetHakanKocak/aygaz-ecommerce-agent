using System.Globalization;
using System.Text.RegularExpressions;
using Aygaz.ECommerce.SemanticKernel.Capabilities;

namespace Aygaz.ECommerce.SemanticKernel.Services;

/// <summary>
/// Resolves capability from the current user message when explicit domain markers are present.
/// Current explicit intent takes precedence over conversation history.
/// </summary>
internal static class ExplicitCapabilityResolver
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Regex CustomerIdPattern = new(
        @"\b(\d{1,6})\s+numaral[ıi]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryResolve(string message, out AygazCapability capability)
    {
        capability = AygazCapability.Unknown;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (AygazCompanyInfoResolver.TryResolve(message, out capability))
        {
            return true;
        }

        string normalized = Normalize(message);

        if (HasExplicitProductInventoryIntent(normalized))
        {
            capability = AygazCapability.ProductInventory;
            return true;
        }

        if (HasExplicitOrderIntent(normalized, message))
        {
            capability = AygazCapability.Order;
            return true;
        }

        if (HasExplicitPolicyIntent(normalized))
        {
            capability = AygazCapability.Policy;
            return true;
        }

        if (HasExplicitCustomerIntent(message))
        {
            capability = AygazCapability.Customer;
            return true;
        }

        return false;
    }

    public static bool HasCustomerId(string message)
    {
        return CustomerIdPattern.IsMatch(Normalize(message));
    }

    public static bool IsCustomerScopedOrderListRequest(string message)
    {
        string normalized = Normalize(message);
        bool hasOrderWord = normalized.Contains("sipariş", StringComparison.Ordinal)
            || normalized.Contains("siparis", StringComparison.Ordinal);

        if (!hasOrderWord)
        {
            return false;
        }

        if (HasCustomerId(message))
        {
            return true;
        }

        return OrderBulkRequestDetector.IsReferentialCustomerScope(message);
    }

    private static bool HasExplicitOrderIntent(string normalized, string originalMessage)
    {
        if (normalized.Contains("ayg-demo-", StringComparison.Ordinal)
            && !normalized.Contains("ayg-demo-prd-", StringComparison.Ordinal))
        {
            return true;
        }

        bool hasOrderWord = normalized.Contains("sipariş", StringComparison.Ordinal)
            || normalized.Contains("siparis", StringComparison.Ordinal);

        if (!hasOrderWord)
        {
            return false;
        }

        if (CustomerIdPattern.IsMatch(normalized))
        {
            return true;
        }

        if (OrderBulkRequestDetector.IsReferentialCustomerScope(originalMessage))
        {
            return true;
        }

        return normalized.Contains("son sipariş", StringComparison.Ordinal)
               || normalized.Contains("son siparis", StringComparison.Ordinal)
               || normalized.Contains("siparişler", StringComparison.Ordinal)
               || normalized.Contains("siparisler", StringComparison.Ordinal)
               || normalized.Contains("siparişi", StringComparison.Ordinal)
               || normalized.Contains("siparisi", StringComparison.Ordinal)
               || normalized.Contains("sipariş bilgi", StringComparison.Ordinal)
               || normalized.Contains("siparis bilgi", StringComparison.Ordinal);
    }

    private static bool HasExplicitProductInventoryIntent(string normalized)
    {
        if (normalized.Contains("ayg-demo-prd-", StringComparison.Ordinal))
        {
            return true;
        }

        return normalized.Contains("stok", StringComparison.Ordinal)
               || normalized.Contains("envanter", StringComparison.Ordinal)
               || normalized.Contains("inventory", StringComparison.Ordinal)
               || normalized.Contains("ürün", StringComparison.Ordinal)
               || normalized.Contains("urun", StringComparison.Ordinal)
               || normalized.Contains("sku", StringComparison.Ordinal)
               || normalized.Contains("kategori", StringComparison.Ordinal);
    }

    private static bool HasExplicitPolicyIntent(string normalized)
    {
        return normalized.Contains("iade", StringComparison.Ordinal)
               || normalized.Contains("teslimat", StringComparison.Ordinal)
               || normalized.Contains("kargo", StringComparison.Ordinal)
               || normalized.Contains("kampanya", StringComparison.Ordinal)
               || normalized.Contains("indirim", StringComparison.Ordinal)
               || normalized.Contains("politika", StringComparison.Ordinal)
               || normalized.Contains("koşul", StringComparison.Ordinal)
               || normalized.Contains("kosul", StringComparison.Ordinal)
               || normalized.Contains("prosedür", StringComparison.Ordinal)
               || normalized.Contains("prosedur", StringComparison.Ordinal);
    }

    private static bool HasExplicitCustomerIntent(string message)
    {
        if (AygazCompanyInfoResolver.TryResolve(message, out _))
        {
            return false;
        }

        string normalized = Normalize(message);

        if (normalized.Contains("ayg-demo-", StringComparison.Ordinal)
            || normalized.Contains("sipariş", StringComparison.Ordinal)
            || normalized.Contains("siparis", StringComparison.Ordinal))
        {
            return false;
        }

        if (CustomerFollowUpResolver.IsFollowUpPhrase(message))
        {
            return false;
        }

        if (normalized.Contains("telefon", StringComparison.Ordinal)
            || normalized.Contains("adres", StringComparison.Ordinal)
            || normalized.Contains("e-posta", StringComparison.Ordinal)
            || normalized.Contains("eposta", StringComparison.Ordinal)
            || normalized.Contains("email", StringComparison.Ordinal))
        {
            return true;
        }

        if (normalized.Contains("müşteri", StringComparison.Ordinal)
            || normalized.Contains("musteri", StringComparison.Ordinal))
        {
            return true;
        }

        if (normalized.Contains("bilgiler", StringComparison.Ordinal)
            || normalized.Contains("nerede yaşıyor", StringComparison.Ordinal)
            || normalized.Contains("nerede yasiyor", StringComparison.Ordinal)
            || normalized.Contains("nerede oturuyor", StringComparison.Ordinal))
        {
            // Generic "bilgi" about Aygaz company must not become Customer.
            if (normalized.Contains("aygaz", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        return ContainsCityScopedCustomerRequest(normalized);
    }

    private static bool ContainsCityScopedCustomerRequest(string normalized)
    {
        return normalized.Contains("istanbul", StringComparison.Ordinal)
               || normalized.Contains("ankara", StringComparison.Ordinal)
               || normalized.Contains("izmir", StringComparison.Ordinal)
               || normalized.Contains("'da", StringComparison.Ordinal)
               || normalized.Contains("'de", StringComparison.Ordinal)
               || normalized.Contains("daki", StringComparison.Ordinal)
               || normalized.Contains("deki", StringComparison.Ordinal)
               || normalized.Contains("yaşayan müşteri", StringComparison.Ordinal)
               || normalized.Contains("yasayan musteri", StringComparison.Ordinal);
    }

    private static string Normalize(string message)
    {
        return message.Trim().ToLower(Turkish);
    }
}
