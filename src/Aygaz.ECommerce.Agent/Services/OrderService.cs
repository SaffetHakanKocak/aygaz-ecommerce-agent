using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class OrderService(IECommerceDataAccess dataAccess) : IOrderService
{
    public Task<OrderDto?> GetOrderByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Task.FromResult<OrderDto?>(null);
        }

        return dataAccess.GetOrderByIdAsync(id, cancellationToken);
    }

    public Task<OrderDto?> GetOrderByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return Task.FromResult<OrderDto?>(null);
        }

        string normalizedOrderNumber = orderNumber.Trim();

        return dataAccess.GetOrderByNumberAsync(normalizedOrderNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(
        int customerId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0 || maxResults <= 0)
        {
            return Array.Empty<OrderDto>();
        }

        return await dataAccess.GetCustomerOrdersAsync(customerId, maxResults, cancellationToken);
    }

    public Task<OrderDto?> GetLatestCustomerOrderAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return Task.FromResult<OrderDto?>(null);
        }

        return dataAccess.GetLatestCustomerOrderAsync(customerId, cancellationToken);
    }
}
