namespace Aygaz.ECommerce.Agent.Models;

public sealed record TopSellingProductDto(
    string Sku,
    string ProductName,
    long QuantitySold,
    decimal Revenue);
