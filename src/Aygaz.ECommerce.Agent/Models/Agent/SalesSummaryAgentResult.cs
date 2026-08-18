namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record SalesSummaryAgentResult(
    decimal TotalRevenue,
    long OrderCount,
    long ItemsSold,
    decimal AverageOrderValue,
    string CurrencyCode);
