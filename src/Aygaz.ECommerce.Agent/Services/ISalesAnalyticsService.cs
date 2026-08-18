using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface ISalesAnalyticsService
{
    Task<SalesSummaryDto> GetSalesSummaryAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(
        int customerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}
