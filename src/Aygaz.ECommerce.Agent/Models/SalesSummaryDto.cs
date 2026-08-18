namespace Aygaz.ECommerce.Agent.Models;

public sealed record SalesSummaryDto(
    decimal TotalRevenue,
    int OrderCount,
    long ItemsSold,
    decimal AverageOrderValue);
