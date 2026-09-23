using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal sealed class ScopedSalesAnalyticsServiceAccessor : ISalesAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedSalesAnalyticsServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<SalesSummaryDto> GetSalesSummaryAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetSalesSummaryAsync(fromDate, toDate, cancellationToken));
    }

    public Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetTopSellingProductsAsync(fromDate, toDate, limit, cancellationToken));
    }

    public Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(
        int customerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetCustomerPurchaseSummaryAsync(customerId, fromDate, toDate, cancellationToken));
    }

    private async Task<T> ExecuteAsync<T>(Func<ISalesAnalyticsService, Task<T>> action)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ISalesAnalyticsService>();
        return await action(service);
    }
}
