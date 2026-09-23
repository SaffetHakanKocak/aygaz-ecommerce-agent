using Aygaz.ECommerce.Agent.Entities;

namespace Aygaz.ECommerce.Agent.Models;

public sealed record OrderOperationResultDto(
    bool Success,
    string Message,
    OrderDto? Order,
    OrderStatus? PreviousStatus,
    OrderStatus? NewStatus,
    string? AuditLogId);
