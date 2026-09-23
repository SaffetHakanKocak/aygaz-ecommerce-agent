using System.Globalization;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal static class OrderMutationIntent
{
    public static bool IsMutation(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string normalized = message.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        if (LooksLikeQuestion(normalized))
        {
            return false;
        }

        return normalized.Contains("yap", StringComparison.Ordinal)
               || normalized.Contains("guncelle", StringComparison.Ordinal)
               || normalized.Contains("güncelle", StringComparison.Ordinal)
               || normalized.Contains("degistir", StringComparison.Ordinal)
               || normalized.Contains("değiştir", StringComparison.Ordinal)
               || normalized.Contains("olsun", StringComparison.Ordinal)
               || normalized.Contains("ayarla", StringComparison.Ordinal)
               || normalized.Contains("isaretle", StringComparison.Ordinal)
               || normalized.Contains("işaretle", StringComparison.Ordinal);
    }

    private static bool LooksLikeQuestion(string normalized)
    {
        return normalized.Contains('?')
               || normalized.Contains(" nedir", StringComparison.Ordinal)
               || normalized.Contains(" neydi", StringComparison.Ordinal)
               || normalized.Contains(" mi", StringComparison.Ordinal)
               || normalized.Contains(" mı", StringComparison.Ordinal)
               || normalized.Contains(" mu", StringComparison.Ordinal)
               || normalized.Contains(" mü", StringComparison.Ordinal);
    }
}
