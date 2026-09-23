using System.ComponentModel;
using System.Globalization;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Plugins;

public sealed class SalesAnalyticsPlugin
{
    private const string IsoDateFormat = "yyyy-MM-dd";

    private readonly ISalesAnalyticsService _salesAnalyticsService;
    private readonly CommerceOptions _options;

    public SalesAnalyticsPlugin(ISalesAnalyticsService salesAnalyticsService, CommerceOptions options)
    {
        ArgumentNullException.ThrowIfNull(salesAnalyticsService);
        ArgumentNullException.ThrowIfNull(options);
        _salesAnalyticsService = salesAnalyticsService;
        _options = options;
    }

    [KernelFunction("get_sales_summary")]
    [Description("Gets sales summary for non-cancelled synthetic Aygaz orders in an inclusive ISO date range.")]
    public async Task<SalesSummaryAgentResult?> GetSalesSummaryAsync(
        [Description("Inclusive start date in yyyy-MM-dd format.")] string fromDate,
        [Description("Inclusive end date in yyyy-MM-dd format.")] string toDate,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseDateRange(fromDate, toDate, out DateOnly from, out DateOnly to))
        {
            return null;
        }

        SalesSummaryDto summary = await _salesAnalyticsService.GetSalesSummaryAsync(from, to, cancellationToken);
        return new SalesSummaryAgentResult(
            summary.TotalRevenue,
            summary.OrderCount,
            summary.ItemsSold,
            summary.AverageOrderValue,
            _options.CurrencyCode);
    }

    [KernelFunction("get_top_selling_products")]
    [Description("Gets top selling products in an inclusive ISO date range.")]
    public async Task<TopSellingProductsAgentResult?> GetTopSellingProductsAsync(
        [Description("Inclusive start date in yyyy-MM-dd format.")] string fromDate,
        [Description("Inclusive end date in yyyy-MM-dd format.")] string toDate,
        [Description("Positive result limit.")] int limit,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseDateRange(fromDate, toDate, out DateOnly from, out DateOnly to)
            || limit <= 0)
        {
            return null;
        }

        int safeLimit = Math.Min(limit, _options.MaximumTopProducts);
        IReadOnlyList<TopSellingProductDto> products = await _salesAnalyticsService.GetTopSellingProductsAsync(
            from,
            to,
            safeLimit,
            cancellationToken);

        return new TopSellingProductsAgentResult(
            _options.CurrencyCode,
            products
                .Take(safeLimit)
                .Select(product => new TopSellingProductAgentResult(
                    product.Sku,
                    product.ProductName,
                    product.QuantitySold,
                    product.Revenue))
                .ToArray());
    }

    [KernelFunction("get_customer_purchase_summary")]
    [Description("Gets one customer's purchase summary in an inclusive ISO date range.")]
    public async Task<CustomerPurchaseSummaryAgentResult?> GetCustomerPurchaseSummaryAsync(
        [Description("Positive customer id.")] int customerId,
        [Description("Inclusive start date in yyyy-MM-dd format.")] string fromDate,
        [Description("Inclusive end date in yyyy-MM-dd format.")] string toDate,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0
            || !TryParseDateRange(fromDate, toDate, out DateOnly from, out DateOnly to))
        {
            return null;
        }

        CustomerPurchaseSummaryDto? summary = await _salesAnalyticsService.GetCustomerPurchaseSummaryAsync(
            customerId,
            from,
            to,
            cancellationToken);

        return summary is null
            ? null
            : new CustomerPurchaseSummaryAgentResult(
                summary.OrderCount,
                summary.TotalSpent,
                summary.ItemsPurchased,
                _options.CurrencyCode);
    }

    private bool TryParseDateRange(
        string fromDate,
        string toDate,
        out DateOnly from,
        out DateOnly to)
    {
        from = default;
        to = default;

        return DateOnly.TryParseExact(fromDate, IsoDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out from)
            && DateOnly.TryParseExact(toDate, IsoDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out to)
            && from <= to
            && to.DayNumber - from.DayNumber < _options.MaximumAnalysisRangeDays;
    }
}
