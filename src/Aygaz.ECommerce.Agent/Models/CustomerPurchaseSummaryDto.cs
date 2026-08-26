namespace Aygaz.ECommerce.Agent.Models;

public sealed record CustomerPurchaseSummaryDto(
    int OrderCount,
    decimal TotalSpent,
    long ItemsPurchased)
{
    public CustomerPurchaseSummaryDto() : this(0, 0m, 0) { }
}
