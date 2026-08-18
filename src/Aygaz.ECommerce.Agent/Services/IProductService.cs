using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface IProductService
{
    Task<ProductDto?> GetProductByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ProductDto?> GetProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> SearchProductsAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);
}
