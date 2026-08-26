using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class ProductService(IECommerceDataAccess dataAccess) : IProductService
{
    public Task<ProductDto?> GetProductByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Task.FromResult<ProductDto?>(null);
        }

        return dataAccess.GetProductByIdAsync(id, cancellationToken);
    }

    public Task<ProductDto?> GetProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return Task.FromResult<ProductDto?>(null);
        }

        string normalizedSku = sku.Trim();

        return dataAccess.GetProductBySkuAsync(normalizedSku, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return Array.Empty<ProductDto>();
        }

        return await dataAccess.SearchProductsAsync(query.Trim(), maxResults, cancellationToken);
    }
}
