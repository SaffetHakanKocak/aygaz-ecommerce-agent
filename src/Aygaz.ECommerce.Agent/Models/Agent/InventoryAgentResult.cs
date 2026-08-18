namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record InventoryAgentResult(
    string LocationCode,
    string LocationName,
    int QuantityAvailable);
