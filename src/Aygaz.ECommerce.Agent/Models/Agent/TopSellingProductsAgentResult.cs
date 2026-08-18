namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record TopSellingProductsAgentResult(
    string CurrencyCode,
    IReadOnlyList<TopSellingProductAgentResult> Products);
