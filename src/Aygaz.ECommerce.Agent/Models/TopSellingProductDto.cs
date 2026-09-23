namespace Aygaz.ECommerce.Agent.Models;

public sealed record TopSellingProductDto(
    string Sku,
    string ProductName,
    long QuantitySold,
    decimal Revenue)
{
    public TopSellingProductDto() : this(string.Empty, string.Empty, 0, 0m) { }
}
