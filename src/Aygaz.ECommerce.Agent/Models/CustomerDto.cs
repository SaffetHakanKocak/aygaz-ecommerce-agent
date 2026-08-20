namespace Aygaz.ECommerce.Agent.Models;

public sealed record CustomerDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Address,
    string? City,
    DateTime CreatedAt);
