using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class SalesAnalyticsService : ISalesAnalyticsService
{
    private const int AbsoluteMaximumAnalysisRangeDays = 366;
    private const int AbsoluteMaximumTopProducts = 10;

    private readonly IECommerceDataAccess dataAccess;
    private readonly CommerceOptions options;

    public SalesAnalyticsService(
        IECommerceDataAccess dataAccess,
        IOptions<CommerceOptions> commerceOptions)
    {
        ArgumentNullException.ThrowIfNull(dataAccess);
        ArgumentNullException.ThrowIfNull(commerceOptions);

        this.dataAccess = dataAccess;
        options = commerceOptions.Value;
    }

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(fromDate, toDate);

        return await dataAccess.GetSalesSummaryAsync(fromDate, toDate, cancellationToken);
    }

    public async Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(fromDate, toDate);
        ValidateLimit(limit);

        return await dataAccess.GetTopSellingProductsAsync(
            fromDate,
            toDate,
            limit,
            cancellationToken);
    }

    public async Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(
        int customerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(customerId),
                "Müşteri kimliği pozitif olmalıdır.");
        }

        ValidateDateRange(fromDate, toDate);

        return await dataAccess.GetCustomerPurchaseSummaryAsync(
            customerId,
            fromDate,
            toDate,
            cancellationToken);
    }

    private void ValidateDateRange(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException(
                "Bitiş tarihi başlangıç tarihinden önce olamaz.",
                nameof(toDate));
        }

        int maximumRangeDays = options.MaximumAnalysisRangeDays;

        if (maximumRangeDays is < 1 or > AbsoluteMaximumAnalysisRangeDays)
        {
            throw new InvalidOperationException(
                $"Commerce:{nameof(CommerceOptions.MaximumAnalysisRangeDays)} "
                + $"1 ile {AbsoluteMaximumAnalysisRangeDays} arasında olmalıdır.");
        }

        int inclusiveDayCount = toDate.DayNumber - fromDate.DayNumber + 1;

        if (inclusiveDayCount > maximumRangeDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toDate),
                $"Analiz aralığı en fazla {maximumRangeDays} gün olabilir.");
        }
    }

    private void ValidateLimit(int limit)
    {
        int maximumTopProducts = options.MaximumTopProducts;

        if (maximumTopProducts is < 1 or > AbsoluteMaximumTopProducts)
        {
            throw new InvalidOperationException(
                $"Commerce:{nameof(CommerceOptions.MaximumTopProducts)} "
                + $"1 ile {AbsoluteMaximumTopProducts} arasında olmalıdır.");
        }

        if (limit < 1 || limit > maximumTopProducts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Ürün limiti 1 ile {maximumTopProducts} arasında olmalıdır.");
        }
    }
}
