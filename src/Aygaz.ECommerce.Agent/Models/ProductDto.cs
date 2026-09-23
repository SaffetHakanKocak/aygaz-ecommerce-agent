namespace Aygaz.ECommerce.Agent.Models;

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    bool IsActive)
{
    public ProductDto()
        : this(0, string.Empty, string.Empty, string.Empty, 0m, false)
    {
    }
}
