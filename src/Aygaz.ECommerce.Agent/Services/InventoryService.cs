using System.Linq.Expressions;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class InventoryService(ECommerceDbContext dbContext) : IInventoryService
{
    private static readonly Expression<Func<InventoryRecord, InventoryDto>> ToDto = record =>
        new InventoryDto(
            record.Id,
            record.ProductId,
            record.LocationCode,
            record.LocationName,
            record.QuantityAvailable,
            record.ReorderLevel,
            record.UpdatedAt);

    public async Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(
        int productId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || maxResults <= 0)
        {
            return Array.Empty<InventoryDto>();
        }

        return await dbContext.InventoryRecords
            .AsNoTracking()
            .Where(record => record.ProductId == productId)
            .OrderBy(record => record.LocationCode)
            .ThenBy(record => record.Id)
            .Take(maxResults)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public Task<long?> GetTotalAvailableStockAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return Task.FromResult<long?>(null);
        }

        return dbContext.InventoryRecords
            .AsNoTracking()
            .Where(record => record.ProductId == productId)
            .GroupBy(_ => 1)
            .Select(group => (long?)group.Sum(
                record => (long)record.QuantityAvailable))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
