using System.Globalization;
using Aygaz.ECommerce.SemanticKernel.Capabilities;

namespace Aygaz.ECommerce.SemanticKernel.Services;

/// <summary>
/// Detects explicit Aygaz company-information questions that are in-domain
/// but have no dedicated business agent (Allowed + Unknown/Sales/Policy/…).
/// </summary>
internal static class AygazCompanyInfoResolver
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool TryResolve(string message, out AygazCapability capability)
    {
        capability = AygazCapability.Unknown;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string normalized = Normalize(message);
        if (!ContainsAygaz(normalized))
        {
            return false;
        }

        if (IsBusinessLookup(normalized))
        {
            return false;
        }

        if (IsSales(normalized))
        {
            capability = AygazCapability.Sales;
            return true;
        }

        if (IsPolicy(normalized))
        {
            capability = AygazCapability.Policy;
            return true;
        }

        if (IsProductInventory(normalized))
        {
            capability = AygazCapability.ProductInventory;
            return true;
        }

        if (IsGeneralCompanyInfo(normalized) || IsGenericAygazCompanyQuestion(normalized))
        {
            capability = AygazCapability.Unknown;
            return true;
        }

        return false;
    }

    private static bool ContainsAygaz(string normalized)
    {
        return normalized.Contains("aygaz", StringComparison.Ordinal);
    }

    private static bool IsBusinessLookup(string normalized)
    {
        return normalized.Contains("müşteri", StringComparison.Ordinal)
               || normalized.Contains("musteri", StringComparison.Ordinal)
               || normalized.Contains("sipariş", StringComparison.Ordinal)
               || normalized.Contains("siparis", StringComparison.Ordinal)
               || normalized.Contains("ayg-demo-", StringComparison.Ordinal);
    }

    private static bool IsSales(string normalized)
    {
        return normalized.Contains("ciro", StringComparison.Ordinal)
               || normalized.Contains("gelir", StringComparison.Ordinal)
               || normalized.Contains("satış rakam", StringComparison.Ordinal)
               || normalized.Contains("satis rakam", StringComparison.Ordinal);
    }

    private static bool IsPolicy(string normalized)
    {
        return normalized.Contains("politika", StringComparison.Ordinal)
               || normalized.Contains("iade", StringComparison.Ordinal)
               || normalized.Contains("garanti", StringComparison.Ordinal);
    }

    private static bool IsProductInventory(string normalized)
    {
        return normalized.Contains("stok", StringComparison.Ordinal)
               || normalized.Contains("ürün", StringComparison.Ordinal)
               || normalized.Contains("urun", StringComparison.Ordinal)
               || normalized.Contains("ayg-demo-prd", StringComparison.Ordinal);
    }

    private static bool IsGeneralCompanyInfo(string normalized)
    {
        return normalized.Contains("ceo", StringComparison.Ordinal)
               || normalized.Contains("kurucu", StringComparison.Ordinal)
               || normalized.Contains("kuruldu", StringComparison.Ordinal)
               || normalized.Contains("kuruluş", StringComparison.Ordinal)
               || normalized.Contains("kurulus", StringComparison.Ordinal)
               || normalized.Contains("genel merkez", StringComparison.Ordinal)
               || normalized.Contains("merkezi nerede", StringComparison.Ordinal)
               || normalized.Contains("merkezi neresi", StringComparison.Ordinal)
               || normalized.Contains("hakkında", StringComparison.Ordinal)
               || normalized.Contains("hakkinda", StringComparison.Ordinal)
               || normalized.Contains("çalışan sayı", StringComparison.Ordinal)
               || normalized.Contains("calisan sayi", StringComparison.Ordinal)
               || normalized.Contains("çalışan sayısı", StringComparison.Ordinal)
               || normalized.Contains("calisan sayisi", StringComparison.Ordinal)
               || normalized.Contains("kaç çalışan", StringComparison.Ordinal)
               || normalized.Contains("kac calisan", StringComparison.Ordinal)
               || normalized.Contains("ofis", StringComparison.Ordinal)
               || normalized.Contains("tarihçe", StringComparison.Ordinal)
               || normalized.Contains("tarihce", StringComparison.Ordinal);
    }

    private static bool IsGenericAygazCompanyQuestion(string normalized)
    {
        // e.g. "Aygaz CEO kim", "Aygaz ne zaman kuruldu" already covered above;
        // catch remaining short Aygaz company questions without business markers.
        return normalized.StartsWith("aygaz", StringComparison.Ordinal)
               && !IsBusinessLookup(normalized)
               && !IsSales(normalized)
               && !IsPolicy(normalized)
               && !IsProductInventory(normalized);
    }

    private static string Normalize(string message)
    {
        return message.Trim().ToLower(Turkish);
    }
}
