namespace Aygaz.ECommerce.Agent.Models;

public sealed record CustomerPurchaseSummaryDto(
    int OrderCount,
    decimal TotalSpent,
    long ItemsPurchased);
