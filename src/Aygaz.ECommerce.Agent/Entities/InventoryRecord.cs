namespace Aygaz.ECommerce.Agent.Entities;

public sealed class InventoryRecord
{
    public const int MaximumLocationCodeLength = 32;
    public const int MaximumLocationNameLength = 100;

    public int Id { get; set; }

    public int ProductId { get; set; }

    public string LocationCode { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public int QuantityAvailable { get; set; }

    public int ReorderLevel { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
}
