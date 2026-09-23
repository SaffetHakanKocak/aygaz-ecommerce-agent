using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal sealed class ScopedProductServiceAccessor : IProductService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedProductServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetProductByIdAsync(id, cancellationToken));
    }

    public Task<ProductDto?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetProductBySkuAsync(sku, cancellationToken));
    }

    public Task<IReadOnlyList<ProductDto>> SearchProductsAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.SearchProductsAsync(query, maxResults, cancellationToken));
    }

    private async Task<T> ExecuteAsync<T>(Func<IProductService, Task<T>> action)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();
        return await action(service);
    }
}
