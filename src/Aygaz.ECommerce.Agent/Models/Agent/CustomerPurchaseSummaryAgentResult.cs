namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record CustomerPurchaseSummaryAgentResult(
    long OrderCount,
    decimal TotalSpent,
    long ItemsPurchased,
    string CurrencyCode);
