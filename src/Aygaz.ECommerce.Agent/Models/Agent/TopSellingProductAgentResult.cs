namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record TopSellingProductAgentResult(
    string Sku,
    string ProductName,
    long QuantitySold,
    decimal Revenue);
