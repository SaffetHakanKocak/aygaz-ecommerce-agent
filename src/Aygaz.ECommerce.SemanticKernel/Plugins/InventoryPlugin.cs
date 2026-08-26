using System.ComponentModel;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Plugins;

public sealed class InventoryPlugin
{
    private readonly IInventoryService _inventoryService;
    private readonly int _maxInventoryLocationResults;

    public InventoryPlugin(IInventoryService inventoryService, int maxInventoryLocationResults = 5)
    {
        ArgumentNullException.ThrowIfNull(inventoryService);
        _inventoryService = inventoryService;
        _maxInventoryLocationResults = Math.Clamp(maxInventoryLocationResults, 1, 20);
    }

    [KernelFunction("get_product_inventory")]
    [Description("Gets location-level available stock for a product id.")]
    public async Task<IReadOnlyList<InventoryAgentResult>> GetProductInventoryAsync(
        [Description("Positive product id.")] int productId,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return [];
        }

        IReadOnlyList<InventoryDto> inventory = await _inventoryService.GetProductInventoryAsync(
            productId,
            _maxInventoryLocationResults,
            cancellationToken);

        return inventory
            .Take(_maxInventoryLocationResults)
            .Select(item => new InventoryAgentResult(
                item.LocationCode,
                item.LocationName,
                item.QuantityAvailable))
            .ToArray();
    }

    [KernelFunction("get_total_product_stock")]
    [Description("Gets total available stock across all locations for a product id.")]
    public async Task<ProductStockAgentResult?> GetTotalProductStockAsync(
        [Description("Positive product id.")] int productId,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return null;
        }

        long? total = await _inventoryService.GetTotalAvailableStockAsync(productId, cancellationToken);
        return total is null ? null : new ProductStockAgentResult(total.Value);
    }
}
