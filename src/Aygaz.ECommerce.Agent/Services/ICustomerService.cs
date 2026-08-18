using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface ICustomerService
{
    Task<CustomerDto?> GetCustomerByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetCustomerByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(
        CancellationToken cancellationToken = default);
}
