namespace Aygaz.ECommerce.Agent.Entities;

public sealed class Product
{
    public const int MaximumSkuLength = 50;
    public const int MaximumNameLength = 150;
    public const int MaximumCategoryLength = 100;

    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<InventoryRecord> InventoryRecords { get; set; } = [];
}
