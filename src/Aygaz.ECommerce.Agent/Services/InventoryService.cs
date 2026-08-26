using Aygaz.ECommerce.Agent.DataAccess;
using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class InventoryService(IECommerceDataAccess dataAccess) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(
        int productId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || maxResults <= 0)
        {
            return Array.Empty<InventoryDto>();
        }

        return await dataAccess.GetProductInventoryAsync(productId, maxResults, cancellationToken);
    }

    public Task<long?> GetTotalAvailableStockAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return Task.FromResult<long?>(null);
        }

        return dataAccess.GetTotalAvailableStockAsync(productId, cancellationToken);
    }
}
