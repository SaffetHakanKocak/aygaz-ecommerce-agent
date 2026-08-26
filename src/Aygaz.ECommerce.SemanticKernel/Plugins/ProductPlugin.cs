using System.ComponentModel;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Plugins;

public sealed class ProductPlugin
{
    private readonly IProductService _productService;
    private readonly int _maxProductSearchResults;

    public ProductPlugin(IProductService productService, int maxProductSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(productService);
        _productService = productService;
        _maxProductSearchResults = Math.Clamp(maxProductSearchResults, 1, 20);
    }

    [KernelFunction("get_product_by_sku")]
    [Description("Gets one synthetic Aygaz product by exact SKU such as AYG-DEMO-PRD-001.")]
    public async Task<ProductAgentResult?> GetProductBySkuAsync(
        [Description("Exact product SKU.")] string sku,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        ProductDto? product = await _productService.GetProductBySkuAsync(sku.Trim(), cancellationToken);
        return product is null ? null : ToResult(product);
    }

    [KernelFunction("search_products")]
    [Description("Searches synthetic Aygaz products by name, SKU, or category.")]
    public async Task<IReadOnlyList<ProductAgentResult>> SearchProductsAsync(
        [Description("Product name, SKU, or category query.")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        IReadOnlyList<ProductDto> products = await _productService.SearchProductsAsync(
            query.Trim(),
            _maxProductSearchResults,
            cancellationToken);

        return products.Take(_maxProductSearchResults).Select(ToResult).ToArray();
    }

    private static ProductAgentResult ToResult(ProductDto product)
    {
        return new ProductAgentResult(
            product.Id,
            product.Sku,
            product.Name,
            product.Category,
            product.UnitPrice,
            product.IsActive);
    }
}
