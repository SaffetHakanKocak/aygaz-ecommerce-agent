using System.Linq.Expressions;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class ProductService(ECommerceDbContext dbContext) : IProductService
{
    private static readonly Expression<Func<Product, ProductDto>> ToDto = product =>
        new ProductDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Category,
            product.UnitPrice,
            product.IsActive);

    public Task<ProductDto?> GetProductByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Task.FromResult<ProductDto?>(null);
        }

        return dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);
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

        return dbContext.Products
            .AsNoTracking()
            .Where(product => product.Sku == normalizedSku)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);
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

        string escapedQuery = EscapeLikePattern(query.Trim());
        string pattern = $"%{escapedQuery}%";

        return await dbContext.Products
            .AsNoTracking()
            .Where(product =>
                EF.Functions.Like(product.Name, pattern, "\\")
                || EF.Functions.Like(product.Sku, pattern, "\\")
                || EF.Functions.Like(product.Category, pattern, "\\"))
            .OrderBy(product => product.Id)
            .Take(maxResults)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
