using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class OrderOperationService(IECommerceDataAccess dataAccess) : IOrderOperationService
{
    public Task<OrderOperationResultDto> CancelOrderAsync(
        string orderNumber,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(reason)
            ? Task.FromResult(InvalidOperationInput())
            : dataAccess.CancelOrderAsync(orderNumber.Trim(), reason.Trim(), NormalizeActor(actor), cancellationToken);
    }

    public Task<OrderOperationResultDto> UpdateOrderStatusAsync(
        string orderNumber,
        OrderStatus newStatus,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(reason)
            ? Task.FromResult(InvalidOperationInput())
            : dataAccess.UpdateOrderStatusAsync(orderNumber.Trim(), newStatus, reason.Trim(), NormalizeActor(actor), cancellationToken);
    }

    public Task<IReadOnlyList<OrderAuditLogDto>> GetOrderAuditLogsAsync(
        string orderNumber,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(orderNumber) || maxResults <= 0
            ? Task.FromResult<IReadOnlyList<OrderAuditLogDto>>([])
            : dataAccess.GetOrderAuditLogsAsync(orderNumber.Trim(), maxResults, cancellationToken);
    }

    private static OrderOperationResultDto InvalidOperationInput() =>
        new(false, "Siparis numarasi ve islem nedeni zorunludur.", null, null, null, null);

    private static string NormalizeActor(string actor) =>
        string.IsNullOrWhiteSpace(actor) ? "semantic-kernel-agent" : actor.Trim();
}
