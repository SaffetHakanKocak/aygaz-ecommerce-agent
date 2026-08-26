using System.Globalization;
using System.Text.RegularExpressions;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal static class ReferentialMessageDetector
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly Regex CustomerIdPattern = new(
        @"\b(\d{1,6})\s+numaral[ıi]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NamedEntityPattern = new(
        @"\b[\p{L}]+\s+[\p{L}]+['’]?(?:nın|nin|nun|nün|nın|nin)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool ContainsExplicitEntityMarker(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string normalized = message.Trim().ToLower(Turkish);
        return normalized.Contains("ayg-demo-", StringComparison.Ordinal)
               || CustomerIdPattern.IsMatch(normalized)
               || NamedEntityPattern.IsMatch(normalized);
    }

    /// <summary>
    /// True when the current message is a short referential follow-up that may use history.
    /// Explicit new topics (order numbers, named customers, Aygaz company info, global bulk) are false.
    /// </summary>
    public static bool IsReferentialFollowUp(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (AygazCompanyInfoResolver.TryResolve(message, out _))
        {
            return false;
        }

        if (OrderBulkRequestDetector.IsGlobalBulk(message))
        {
            return false;
        }

        if (ContainsExplicitEntityMarker(message))
        {
            return false;
        }

        return CustomerFollowUpResolver.IsFollowUpPhrase(message)
               || OrderFollowUpResolver.IsFollowUpPhrase(message)
               || OrderBulkRequestDetector.IsReferentialCustomerScope(message);
    }
}
