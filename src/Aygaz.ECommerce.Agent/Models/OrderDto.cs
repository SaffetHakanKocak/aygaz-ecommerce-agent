using Aygaz.ECommerce.Agent.Entities;

namespace Aygaz.ECommerce.Agent.Models;

public sealed record OrderDto(
    int Id,
    string OrderNumber,
    int CustomerId,
    DateTime OrderDate,
    OrderStatus Status,
    decimal TotalAmount)
{
    public OrderDto()
        : this(0, string.Empty, 0, default, Entities.OrderStatus.Pending, 0m)
    {
    }
}
