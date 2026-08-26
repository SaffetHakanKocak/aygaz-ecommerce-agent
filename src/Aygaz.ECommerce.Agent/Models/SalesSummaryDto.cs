namespace Aygaz.ECommerce.Agent.Models;

public sealed record SalesSummaryDto(
    decimal TotalRevenue,
    int OrderCount,
    long ItemsSold,
    decimal AverageOrderValue)
{
    public SalesSummaryDto() : this(0m, 0, 0, 0m) { }
}
