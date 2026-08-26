using System.Globalization;

namespace Aygaz.ECommerce.SemanticKernel.Services;

/// <summary>
/// Distinguishes global order bulk listing from customer-scoped / referential lists.
/// </summary>
internal static class OrderBulkRequestDetector
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool IsGlobalBulk(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (IsCustomerScoped(message))
        {
            return false;
        }

        // True referential full-info follow-ups are not global bulk.
        if (OrderFollowUpResolver.IsFollowUpPhrase(message)
            && !LooksLikeGlobalBulkPhrase(Normalize(message)))
        {
            return false;
        }

        return LooksLikeGlobalBulkPhrase(Normalize(message));
    }

    public static bool IsCustomerScoped(string message)
    {
        if (ExplicitCapabilityResolver.IsCustomerScopedOrderListRequest(message))
        {
            return true;
        }

        string normalized = Normalize(message);
        bool hasOrderWord = normalized.Contains("sipariş", StringComparison.Ordinal)
            || normalized.Contains("siparis", StringComparison.Ordinal);

        if (!hasOrderWord)
        {
            return false;
        }

        return IsReferentialCustomerScope(normalized);
    }

    public static bool IsReferentialCustomerScope(string message)
    {
        return IsReferentialCustomerScopeNormalized(Normalize(message));
    }

    private static bool IsReferentialCustomerScopeNormalized(string normalized)
    {
        return normalized.Contains("bu müşterinin", StringComparison.Ordinal)
               || normalized.Contains("bu musterinin", StringComparison.Ordinal)
               || normalized.Contains("onun sipariş", StringComparison.Ordinal)
               || normalized.Contains("onun siparis", StringComparison.Ordinal);
    }

    private static bool LooksLikeGlobalBulkPhrase(string normalized)
    {
        bool hasOrderWord = normalized.Contains("sipariş", StringComparison.Ordinal)
            || normalized.Contains("siparis", StringComparison.Ordinal);

        if (!hasOrderWord)
        {
            return false;
        }

        bool hasAllQuantifier = normalized.Contains("tüm", StringComparison.Ordinal)
            || normalized.Contains("tum", StringComparison.Ordinal)
            || normalized.Contains("bütün", StringComparison.Ordinal)
            || normalized.Contains("butun", StringComparison.Ordinal);

        if (!hasAllQuantifier)
        {
            return normalized.Contains("sipariş listesi", StringComparison.Ordinal)
                   || normalized.Contains("siparis listesi", StringComparison.Ordinal);
        }

        // "tüm siparişleri …", "tüm sipariş bilgilerini …", "bütün siparişlerin …"
        return normalized.Contains("tüm sipariş", StringComparison.Ordinal)
               || normalized.Contains("tum siparis", StringComparison.Ordinal)
               || normalized.Contains("bütün sipariş", StringComparison.Ordinal)
               || normalized.Contains("butun siparis", StringComparison.Ordinal)
               || normalized.Contains("tüm siparişler", StringComparison.Ordinal)
               || normalized.Contains("tum siparisler", StringComparison.Ordinal);
    }

    private static string Normalize(string message)
    {
        return message.Trim().ToLower(Turkish);
    }
}
