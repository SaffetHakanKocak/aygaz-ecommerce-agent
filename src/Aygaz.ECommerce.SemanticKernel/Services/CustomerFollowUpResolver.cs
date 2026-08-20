using System.Globalization;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Services;

/// <summary>
/// Resolves pronoun/omitted-identity customer follow-ups against bounded session history.
/// </summary>
internal static class CustomerFollowUpResolver
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] FollowUpPhrases =
    [
        "tüm bilgilerini getir",
        "tum bilgilerini getir",
        "bilgilerini getir",
        "tüm bilgileri neydi",
        "tum bilgileri neydi",
        "telefonu neydi",
        "telefon numarası",
        "telefon numarasi",
        "adresi neydi",
        "e-postası neydi",
        "epostası neydi",
        "e-postasi neydi",
        "hangi şehirdeydi",
        "hangi sehirdeydi",
        "nerede yaşıyordu",
        "nerede yasiyordu",
        "nerede yaşıyor",
        "nerede yasiyor",
        "nerede oturuyor",
        "müşteri numarası neydi",
        "musteri numarasi neydi"
    ];

    public static bool IsFollowUpPhrase(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string normalized = Normalize(message);
        return FollowUpPhrases.Any(phrase =>
            normalized == phrase
            || normalized.StartsWith(phrase + "?", StringComparison.Ordinal)
            || normalized.Contains(phrase, StringComparison.Ordinal));
    }

    public static bool HistoryHasCustomerReference(ChatHistory? history)
    {
        if (history is null || history.Count == 0)
        {
            return false;
        }

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            if (string.IsNullOrWhiteSpace(msg.Content))
            {
                continue;
            }

            string content = msg.Content.ToLower(Turkish);
            if (content.Contains("müşteri", StringComparison.Ordinal)
                || content.Contains("musteri", StringComparison.Ordinal)
                || content.Contains("müşteri no", StringComparison.Ordinal)
                || content.Contains("ad soyad", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsCustomerFollowUp(ChatHistory? history, string message)
    {
        return IsFollowUpPhrase(message) && HistoryHasCustomerReference(history);
    }

    private static string Normalize(string message)
    {
        return message.Trim().ToLower(Turkish).TrimEnd('?', '.', '!');
    }
}
