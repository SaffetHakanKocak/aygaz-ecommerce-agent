namespace Aygaz.ECommerce.Agent.Models;

public sealed record InventoryDto(
    int Id,
    int ProductId,
    string LocationCode,
    string LocationName,
    int QuantityAvailable,
    int ReorderLevel,
    DateTime UpdatedAt)
{
    public InventoryDto()
        : this(0, 0, string.Empty, string.Empty, 0, 0, default)
    {
    }
}
