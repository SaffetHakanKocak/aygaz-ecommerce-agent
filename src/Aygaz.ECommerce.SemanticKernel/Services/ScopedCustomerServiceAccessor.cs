using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal sealed class ScopedCustomerServiceAccessor : ICustomerService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedCustomerServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<CustomerDto?> GetCustomerByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetCustomerByIdAsync(id, cancellationToken));
    }

    public Task<CustomerDto?> GetCustomerByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetCustomerByEmailAsync(email, cancellationToken));
    }

    public Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.SearchCustomersByNameAsync(searchTerm, cancellationToken));
    }

    public Task<IReadOnlyList<CustomerDto>> SearchCustomersByCityAsync(
        string city,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.SearchCustomersByCityAsync(city, cancellationToken));
    }

    public Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetAllCustomersAsync(cancellationToken));
    }

    private async Task<T> ExecuteAsync<T>(Func<ICustomerService, Task<T>> action)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        return await action(service);
    }
}
