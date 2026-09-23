namespace Aygaz.ECommerce.Agent.Models;

public sealed record OrderDetailDto(
    OrderDto Order,
    IReadOnlyList<OrderItemDto> Items)
{
    public OrderDetailDto() : this(new OrderDto(), []) { }
}

public sealed record OrderItemDto(
    int Id,
    int ProductId,
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal)
{
    public OrderItemDto() : this(0, 0, string.Empty, string.Empty, 0, 0m, 0m) { }
}
