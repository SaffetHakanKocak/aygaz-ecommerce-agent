using System.Globalization;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Services;

/// <summary>
/// Resolves pronoun/omitted-identity order follow-ups against bounded session history.
/// </summary>
internal static class OrderFollowUpResolver
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] FollowUpPhrases =
    [
        "durumu neydi",
        "durumu ne",
        "sipariş numarası neydi",
        "siparis numarasi neydi",
        "tüm bilgilerini getir",
        "tum bilgilerini getir",
        "bilgilerini getir",
        "tüm bilgileri neydi",
        "tum bilgileri neydi"
    ];

    public static bool IsFollowUpPhrase(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (ReferentialMessageDetector.ContainsExplicitEntityMarker(message))
        {
            return false;
        }

        string normalized = Normalize(message);
        return FollowUpPhrases.Any(phrase =>
            normalized == phrase
            || normalized.StartsWith(phrase + "?", StringComparison.Ordinal)
            || normalized.Contains(phrase, StringComparison.Ordinal));
    }

    public static bool HistoryHasOrderReference(ChatHistory? history)
    {
        return ConversationContextResolver.GetMostRecentEntityContext(history)
            == ConversationEntityContext.Order;
    }

    public static bool IsOrderFollowUp(ChatHistory? history, string message)
    {
        if (ExplicitCapabilityResolver.TryResolve(message, out _))
        {
            return false;
        }

        return IsFollowUpPhrase(message) && HistoryHasOrderReference(history);
    }

    private static string Normalize(string message)
    {
        return message.Trim().ToLower(Turkish).TrimEnd('?', '.', '!');
    }
}
