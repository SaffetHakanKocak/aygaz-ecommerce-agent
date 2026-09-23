using Aygaz.ECommerce.Agent.Entities;

namespace Aygaz.ECommerce.Agent.Models;

public sealed record OrderAuditLogDto(
    string Id,
    int OrderId,
    string OrderNumber,
    string Operation,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    string Reason,
    string Actor,
    DateTime CreatedAt);
