using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface IOrderOperationService
{
    Task<OrderOperationResultDto> CancelOrderAsync(
        string orderNumber,
        string reason,
        string actor,
        CancellationToken cancellationToken = default);

    Task<OrderOperationResultDto> UpdateOrderStatusAsync(
        string orderNumber,
        OrderStatus newStatus,
        string reason,
        string actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderAuditLogDto>> GetOrderAuditLogsAsync(
        string orderNumber,
        int maxResults,
        CancellationToken cancellationToken = default);
}
