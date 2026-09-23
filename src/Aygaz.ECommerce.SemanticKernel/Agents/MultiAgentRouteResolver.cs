using System.Globalization;
using Aygaz.ECommerce.SemanticKernel.Capabilities;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

internal static class MultiAgentRouteResolver
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string? ResolveRouteKey(AygazCapability capability, string userMessage)
    {
        return capability switch
        {
            AygazCapability.Customer => CustomerAgentRegistration.RouteKey,
            AygazCapability.Order => OrderAgentRegistration.RouteKey,
            AygazCapability.ProductInventory => ResolveProductInventoryRoute(userMessage),
            AygazCapability.Sales => SalesAnalyticsAgentRegistration.RouteKey,
            AygazCapability.Policy => SupportPolicyAgentRegistration.RouteKey,
            _ => null
        };
    }

    private static string ResolveProductInventoryRoute(string userMessage)
    {
        string normalized = userMessage.Trim().ToLower(Turkish);
        return normalized.Contains("stok", StringComparison.Ordinal)
            || normalized.Contains("envanter", StringComparison.Ordinal)
            || normalized.Contains("inventory", StringComparison.Ordinal)
            || normalized.Contains("mevcut", StringComparison.Ordinal)
            ? InventoryAgentRegistration.RouteKey
            : ProductAgentRegistration.RouteKey;
    }
}
