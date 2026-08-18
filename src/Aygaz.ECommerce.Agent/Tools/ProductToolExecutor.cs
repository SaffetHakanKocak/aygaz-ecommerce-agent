using System.Collections.ObjectModel;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class ProductToolExecutor : IAgentToolModule
{
    public const string GetProductBySkuToolName = "get_product_by_sku";
    public const string SearchProductsToolName = "search_products";

    private const int MaximumSearchQueryLength = 100;

    private static readonly IReadOnlyList<OllamaToolDefinition> Definitions =
        Array.AsReadOnly(new OllamaToolDefinition[]
        {
            CreateToolDefinition(
                GetProductBySkuToolName,
                "AYG-DEMO-PRD-001 gibi tam SKU verilmişse kullanılması zorunlu olan exact lookup'tır. "
                    + "SKU içeren istekte search_products kullanma. Yalnız kullanıcı ayrıca stok veya inventory "
                    + "istediğinde sonuçtaki ürün ID'siyle inventory tool'u çağır; yalnız ürün bulma "
                    + "isteğinde başka tool çağırma.",
                "sku",
                "string",
                "Aranacak sentetik ürünün SKU değeri."),
            CreateToolDefinition(
                SearchProductsToolName,
                "Ürün adı veya kategori ile sınırlı sayıda sentetik ürün ve ürün ID'si bulur. Tam "
                    + "AYG-DEMO-PRD-001 biçiminde SKU verilmişse bunu kullanma; get_product_by_sku kullan. "
                    + "Kullanıcı stok soruyorsa bu sonuç final değildir: tek ve açık eşleşmenin ID'siyle "
                    + "hemen inventory tool'unu çağır. Yalnız ürün bulma isteğinde inventory çağırma. Birden fazla "
                    + "eşleşmede kullanıcıdan netleştirme iste.",
                "query",
                "string",
                "Aranacak ürün adı, SKU veya kategori metni.")
        });

    private readonly IProductService _productService;
    private readonly AgentOptions _options;
    private readonly IToolCallLogger _logger;

    public ProductToolExecutor(
        IProductService productService,
        IOptions<AgentOptions> options,
        IToolCallLogger logger)
    {
        ArgumentNullException.ThrowIfNull(productService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (options.Value.MaxProductSearchResults is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Agent:MaxProductSearchResults 1 ile 20 arasında olmalıdır.");
        }

        _productService = productService;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<OllamaToolDefinition> ToolDefinitions => Definitions;

    public async Task<ToolExecutionResult> ExecuteAsync(
        string? toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogToolCall(toolName);

        ToolExecutionResult result = toolName switch
        {
            GetProductBySkuToolName =>
                await ExecuteGetBySkuAsync(arguments, cancellationToken),
            SearchProductsToolName =>
                await ExecuteSearchAsync(arguments, cancellationToken),
            _ => ToolExecutionResult.Rejected("İstenen tool kullanılamıyor.")
        };

        _logger.LogResult(result.Status);
        return result;
    }

    private async Task<ToolExecutionResult> ExecuteGetBySkuAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetOnlyArgument(arguments, "sku", out JsonElement skuElement)
            || skuElement.ValueKind != JsonValueKind.String)
        {
            _logger.LogArguments("sku", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir SKU belirtilmedi.");
        }

        string sku = skuElement.GetString()?.Trim() ?? string.Empty;
        _logger.LogArguments("sku", sku);

        if (sku.Length is 0 or > Product.MaximumSkuLength)
        {
            return ToolExecutionResult.Rejected(
                "Geçerli bir SKU belirtilmedi.");
        }

        ProductDto? product = await _productService.GetProductBySkuAsync(
            sku,
            cancellationToken);

        return product is null
            ? ToolExecutionResult.ProductNotFound()
            : ToolExecutionResult.FromSuccess(ToAgentResult(product));
    }

    private async Task<ToolExecutionResult> ExecuteSearchAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetOnlyArgument(arguments, "query", out JsonElement queryElement)
            || queryElement.ValueKind != JsonValueKind.String)
        {
            _logger.LogArguments("query", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir ürün arama sorgusu belirtilmedi.");
        }

        string query = queryElement.GetString()?.Trim() ?? string.Empty;
        _logger.LogArguments("query", query);

        if (query.Length is 0 or > MaximumSearchQueryLength)
        {
            return ToolExecutionResult.Rejected(
                "Geçerli bir ürün arama sorgusu belirtilmedi.");
        }

        IReadOnlyList<ProductDto> products =
            await _productService.SearchProductsAsync(
                query,
                _options.MaxProductSearchResults,
                cancellationToken);

        ProductAgentResult[] results = products
            .Take(_options.MaxProductSearchResults)
            .Select(ToAgentResult)
            .ToArray();

        return results.Length == 0
            ? ToolExecutionResult.ProductNotFound()
            : ToolExecutionResult.FromSuccess(results);
    }

    private static bool TryGetOnlyArgument(
        JsonElement arguments,
        string expectedName,
        out JsonElement value)
    {
        value = default;

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        int propertyCount = 0;

        foreach (JsonProperty property in arguments.EnumerateObject())
        {
            propertyCount++;

            if (propertyCount > 1
                || !property.Name.Equals(expectedName, StringComparison.Ordinal))
            {
                return false;
            }

            value = property.Value;
        }

        return propertyCount == 1;
    }

    private static ProductAgentResult ToAgentResult(ProductDto product)
    {
        return new ProductAgentResult(
            product.Id,
            product.Sku,
            product.Name,
            product.Category,
            product.UnitPrice,
            product.IsActive);
    }

    private static OllamaToolDefinition CreateToolDefinition(
        string name,
        string description,
        string argumentName,
        string argumentType,
        string argumentDescription)
    {
        var properties = new ReadOnlyDictionary<string, OllamaToolProperty>(
            new Dictionary<string, OllamaToolProperty>(StringComparer.Ordinal)
            {
                [argumentName] = new OllamaToolProperty(
                    argumentType,
                    argumentDescription)
            });

        return new OllamaToolDefinition(
            "function",
            new OllamaToolFunctionDefinition(
                name,
                description,
                new OllamaToolParameters(
                    "object",
                    properties,
                    Array.AsReadOnly(new[] { argumentName }),
                    AdditionalProperties: false)));
    }
}
