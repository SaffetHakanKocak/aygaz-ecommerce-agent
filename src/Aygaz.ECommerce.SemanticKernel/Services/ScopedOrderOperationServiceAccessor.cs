using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal sealed class ScopedOrderOperationServiceAccessor : IOrderOperationService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedOrderOperationServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<OrderOperationResultDto> CancelOrderAsync(
        string orderNumber,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.CancelOrderAsync(orderNumber, reason, actor, cancellationToken));
    }

    public Task<OrderOperationResultDto> UpdateOrderStatusAsync(
        string orderNumber,
        OrderStatus newStatus,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.UpdateOrderStatusAsync(orderNumber, newStatus, reason, actor, cancellationToken));
    }

    public Task<IReadOnlyList<OrderAuditLogDto>> GetOrderAuditLogsAsync(
        string orderNumber,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetOrderAuditLogsAsync(orderNumber, maxResults, cancellationToken));
    }

    private async Task<T> ExecuteAsync<T>(Func<IOrderOperationService, Task<T>> action)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderOperationService>();
        return await action(service);
    }
}
