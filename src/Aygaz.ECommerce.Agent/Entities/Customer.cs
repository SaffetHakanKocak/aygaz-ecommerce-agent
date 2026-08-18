namespace Aygaz.ECommerce.Agent.Entities;

public sealed class Customer
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? City { get; set; }

    public DateTime CreatedAt { get; set; }
}
