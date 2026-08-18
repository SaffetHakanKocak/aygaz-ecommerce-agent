namespace Aygaz.ECommerce.Agent.Models;

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    bool IsActive);
