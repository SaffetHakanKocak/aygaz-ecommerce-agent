using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal sealed class ScopedInventoryServiceAccessor : IInventoryService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedInventoryServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(
        int productId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetProductInventoryAsync(productId, maxResults, cancellationToken));
    }

    public Task<long?> GetTotalAvailableStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetTotalAvailableStockAsync(productId, cancellationToken));
    }

    private async Task<T> ExecuteAsync<T>(Func<IInventoryService, Task<T>> action)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        return await action(service);
    }
}
