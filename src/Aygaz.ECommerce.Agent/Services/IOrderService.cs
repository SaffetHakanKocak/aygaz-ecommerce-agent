using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface IOrderService
{
    Task<OrderDto?> GetOrderByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetOrderByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(
        int customerId,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetLatestCustomerOrderAsync(
        int customerId,
        CancellationToken cancellationToken = default);
}
