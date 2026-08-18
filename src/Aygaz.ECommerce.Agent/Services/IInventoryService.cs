using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(
        int productId,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<long?> GetTotalAvailableStockAsync(
        int productId,
        CancellationToken cancellationToken = default);
}
