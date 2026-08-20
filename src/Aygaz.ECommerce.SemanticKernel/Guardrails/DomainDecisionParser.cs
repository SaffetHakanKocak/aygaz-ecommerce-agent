using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

internal static class DomainDecisionParser
{
    private static readonly Regex DecisionProperty = new(
        """["']decision["']\s*:\s*["'](Allowed|OutOfScope|Ambiguous)["']""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryParse(string? content, out DomainDecision decision)
    {
        decision = DomainDecision.Ambiguous;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        string trimmed = content.Trim();
        if (TryMapToken(trimmed, out decision))
        {
            return true;
        }

        if (TryParseJsonObject(trimmed, out decision))
        {
            return true;
        }

        Match match = DecisionProperty.Match(trimmed);
        return match.Success && TryMapToken(match.Groups[1].Value, out decision);
    }

    private static bool TryParseJsonObject(string content, out DomainDecision decision)
    {
        decision = DomainDecision.Ambiguous;
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("decision", out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return TryMapToken(value.GetString(), out decision);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryMapToken(string? token, out DomainDecision decision)
    {
        switch (token)
        {
            case "Allowed":
                decision = DomainDecision.Allowed;
                return true;
            case "OutOfScope":
                decision = DomainDecision.OutOfScope;
                return true;
            case "Ambiguous":
                decision = DomainDecision.Ambiguous;
                return true;
            default:
                decision = DomainDecision.Ambiguous;
                return false;
        }
    }
}
