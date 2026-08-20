namespace Aygaz.ECommerce.Agent.Models.Agent;

public sealed record CustomerAgentResult(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? City,
    string? PhoneNumber = null,
    string? Address = null);
