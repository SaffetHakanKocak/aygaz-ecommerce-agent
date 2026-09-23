using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal sealed class ScopedOrderServiceAccessor : IOrderService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedOrderServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<OrderDto?> GetOrderByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetOrderByIdAsync(id, cancellationToken));
    }

    public Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetOrderByNumberAsync(orderNumber, cancellationToken));
    }

    public Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(
        int customerId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetCustomerOrdersAsync(customerId, maxResults, cancellationToken));
    }

    public Task<OrderDto?> GetLatestCustomerOrderAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetLatestCustomerOrderAsync(customerId, cancellationToken));
    }

    private async Task<T> ExecuteAsync<T>(Func<IOrderService, Task<T>> action)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
        return await action(service);
    }
}
