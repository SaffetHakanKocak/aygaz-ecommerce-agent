using System.Text.Json;
using System.Text.RegularExpressions;
using Aygaz.ECommerce.SemanticKernel.Capabilities;

namespace Aygaz.ECommerce.SemanticKernel.Guardrails;

internal static class DomainDecisionParser
{
    private static readonly Regex DecisionProperty = new(
        """["']decision["']\s*:\s*["']([^"']+)["']""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CapabilityProperty = new(
        """["']capability["']\s*:\s*["']([^"']+)["']""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParse(
        string? content,
        out DomainDecision decision,
        out AygazCapability capability,
        out string? reason)
    {
        decision = DomainDecision.Ambiguous;
        capability = AygazCapability.Unknown;
        reason = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        string trimmed = content.Trim();
        if (TryMapToken(trimmed, out decision))
        {
            return true;
        }

        if (TryParseJsonObject(trimmed, out decision, out capability, out reason))
        {
            return true;
        }

        Match match = DecisionProperty.Match(trimmed);
        if (!match.Success || !TryMapToken(match.Groups[1].Value, out decision))
        {
            return false;
        }

        Match capabilityMatch = CapabilityProperty.Match(trimmed);
        if (capabilityMatch.Success)
        {
            _ = TryMapCapability(capabilityMatch.Groups[1].Value, out capability);
        }

        return true;
    }

    private static bool TryParseJsonObject(
        string content,
        out DomainDecision decision,
        out AygazCapability capability,
        out string? reason)
    {
        decision = DomainDecision.Ambiguous;
        capability = AygazCapability.Unknown;
        reason = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("decision", out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!TryMapToken(value.GetString(), out decision))
            {
                return false;
            }

            if (document.RootElement.TryGetProperty("capability", out JsonElement capabilityValue)
                && capabilityValue.ValueKind == JsonValueKind.String)
            {
                _ = TryMapCapability(capabilityValue.GetString(), out capability);
            }

            if (document.RootElement.TryGetProperty("reason", out JsonElement reasonValue)
                && reasonValue.ValueKind == JsonValueKind.String)
            {
                reason = reasonValue.GetString();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryMapToken(string? token, out DomainDecision decision)
    {
        string normalized = token?.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant() ?? string.Empty;
        switch (normalized)
        {
            case "allowed":
                decision = DomainDecision.Allowed;
                return true;
            case "outofscope":
            case "disallowed":
                decision = DomainDecision.OutOfScope;
                return true;
            case "ambiguous":
            case "uncertain":
                decision = DomainDecision.Ambiguous;
                return true;
            default:
                decision = DomainDecision.Ambiguous;
                return false;
        }
    }

    private static bool TryMapCapability(string? token, out AygazCapability capability)
    {
        string normalized = token?.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant() ?? string.Empty;
        switch (normalized)
        {
            case "customer":
                capability = AygazCapability.Customer;
                return true;
            case "order":
                capability = AygazCapability.Order;
                return true;
            case "productinventory":
            case "inventory":
            case "product":
                capability = AygazCapability.ProductInventory;
                return true;
            case "sales":
                capability = AygazCapability.Sales;
                return true;
            case "policy":
                capability = AygazCapability.Policy;
                return true;
            default:
                capability = AygazCapability.Unknown;
                return false;
        }
    }
}
