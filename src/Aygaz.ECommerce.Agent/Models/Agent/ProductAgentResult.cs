namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record ProductAgentResult(
    int Id,
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    bool IsActive);
