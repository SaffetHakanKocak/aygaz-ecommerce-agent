using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class SalesAnalyticsService : ISalesAnalyticsService
{
    private const int AbsoluteMaximumAnalysisRangeDays = 366;
    private const int AbsoluteMaximumTopProducts = 10;

    private readonly ECommerceDbContext dbContext;
    private readonly CommerceOptions options;

    public SalesAnalyticsService(
        ECommerceDbContext dbContext,
        IOptions<CommerceOptions> commerceOptions)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(commerceOptions);

        this.dbContext = dbContext;
        options = commerceOptions.Value;
    }

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(fromDate, toDate);

        IQueryable<CustomerOrder> eligibleOrders = CreateEligibleOrdersQuery(
            fromDate,
            toDate);

        int orderCount = await eligibleOrders.CountAsync(cancellationToken);
        ItemAggregate? itemAggregate = await CreateItemAggregateQuery(eligibleOrders)
            .SingleOrDefaultAsync(cancellationToken);

        decimal totalRevenue = itemAggregate?.Revenue ?? 0m;
        long itemsSold = itemAggregate?.Quantity ?? 0L;
        decimal averageOrderValue = orderCount == 0
            ? 0m
            : Math.Round(
                totalRevenue / orderCount,
                2,
                MidpointRounding.AwayFromZero);

        return new SalesSummaryDto(
            totalRevenue,
            orderCount,
            itemsSold,
            averageOrderValue);
    }

    public async Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(fromDate, toDate);
        ValidateLimit(limit);

        IQueryable<OrderItem> eligibleItems = CreateEligibleOrdersQuery(
                fromDate,
                toDate)
            .SelectMany(order => order.OrderItems);

        List<TopSellingProductDto> productAggregates = await eligibleItems
            .GroupBy(item => new
            {
                item.ProductId,
                item.Product.Sku,
                item.Product.Name
            })
            .Select(group => new TopSellingProductDto(
                group.Key.Sku,
                group.Key.Name,
                group.Sum(item => (long)item.Quantity),
                group.Sum(item => item.UnitPrice * item.Quantity)))
            .ToListAsync(cancellationToken);

        return productAggregates
            .OrderByDescending(product => product.QuantitySold)
            .ThenByDescending(product => product.Revenue)
            .ThenBy(product => product.Sku, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
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

        bool customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.Id == customerId, cancellationToken);

        if (!customerExists)
        {
            return null;
        }

        IQueryable<CustomerOrder> eligibleOrders = CreateEligibleOrdersQuery(
                fromDate,
                toDate)
            .Where(order => order.CustomerId == customerId);

        int orderCount = await eligibleOrders.CountAsync(cancellationToken);
        ItemAggregate? itemAggregate = await CreateItemAggregateQuery(eligibleOrders)
            .SingleOrDefaultAsync(cancellationToken);

        return new CustomerPurchaseSummaryDto(
            orderCount,
            itemAggregate?.Revenue ?? 0m,
            itemAggregate?.Quantity ?? 0L);
    }

    private IQueryable<CustomerOrder> CreateEligibleOrdersQuery(
        DateOnly fromDate,
        DateOnly toDate)
    {
        DateTime fromDateTime = DateTime.SpecifyKind(
            fromDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        IQueryable<CustomerOrder> query = dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.Status != OrderStatus.Cancelled)
            .Where(order => order.OrderDate >= fromDateTime);

        if (toDate == DateOnly.MaxValue)
        {
            return query;
        }

        DateTime toDateExclusive = DateTime.SpecifyKind(
            toDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        return query.Where(order => order.OrderDate < toDateExclusive);
    }

    private static IQueryable<ItemAggregate> CreateItemAggregateQuery(
        IQueryable<CustomerOrder> eligibleOrders)
    {
        return eligibleOrders
            .SelectMany(order => order.OrderItems)
            .GroupBy(_ => 1)
            .Select(group => new ItemAggregate(
                group.Sum(item => item.UnitPrice * item.Quantity),
                group.Sum(item => (long)item.Quantity)));
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

    private sealed record ItemAggregate(decimal Revenue, long Quantity);
}
