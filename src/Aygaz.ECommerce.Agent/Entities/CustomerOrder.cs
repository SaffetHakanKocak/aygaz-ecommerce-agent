namespace Aygaz.ECommerce.Agent.Entities;

public sealed class CustomerOrder
{
    public const int MaximumOrderNumberLength = 50;

    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    public OrderStatus Status { get; set; }

    public decimal TotalAmount { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
