namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record OrderAgentResult(
    int Id,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount);
