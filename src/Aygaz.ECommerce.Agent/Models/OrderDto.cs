using Aygaz.ECommerce.Agent.Entities;

namespace Aygaz.ECommerce.Agent.Models;

public sealed record OrderDto(
    int Id,
    string OrderNumber,
    int CustomerId,
    DateTime OrderDate,
    OrderStatus Status,
    decimal TotalAmount);
