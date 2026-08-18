using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class InventoryToolExecutor : IAgentToolModule
{
    public const string GetProductInventoryToolName = "get_product_inventory";
    public const string GetTotalProductStockToolName = "get_total_product_stock";

    private static readonly IReadOnlyList<OllamaToolDefinition> Definitions =
        Array.AsReadOnly(new OllamaToolDefinition[]
        {
            CreateToolDefinition(
                GetProductInventoryToolName,
                "Belirli bir ürün ID'si için sınırlı sayıda lokasyon bazlı kullanılabilir stok bilgisini getirir. "
                    + "Ürün ID'si bilinmiyorsa önce get_product_by_sku veya search_products tool'unu kullan.",
                "productId",
                "integer",
                "Stok kayıtları aranacak ürünün pozitif kimlik numarası."),
            CreateToolDefinition(
                GetTotalProductStockToolName,
                "Belirli bir ürün ID'sinin tüm lokasyonlardaki toplam kullanılabilir stok miktarını getirir. "
                    + "Ürün ID'si bilinmiyorsa önce get_product_by_sku veya search_products tool'unu kullan.",
                "productId",
                "integer",
                "Toplam stoğu aranacak ürünün pozitif kimlik numarası.")
        });

    private readonly IInventoryService _inventoryService;
    private readonly AgentOptions _options;
    private readonly IToolCallLogger _logger;

    public InventoryToolExecutor(
        IInventoryService inventoryService,
        IOptions<AgentOptions> options,
        IToolCallLogger logger)
    {
        ArgumentNullException.ThrowIfNull(inventoryService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (options.Value.MaxInventoryLocationResults is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Agent:MaxInventoryLocationResults 1 ile 20 arasında olmalıdır.");
        }

        _inventoryService = inventoryService;
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
            GetProductInventoryToolName =>
                await ExecuteGetProductInventoryAsync(arguments, cancellationToken),
            GetTotalProductStockToolName =>
                await ExecuteGetTotalProductStockAsync(arguments, cancellationToken),
            _ => ToolExecutionResult.Rejected("İstenen tool kullanılamıyor.")
        };

        _logger.LogResult(result.Status);
        return result;
    }

    private async Task<ToolExecutionResult> ExecuteGetProductInventoryAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetPositiveProductId(arguments, out int productId))
        {
            _logger.LogArguments("productId", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir ürün kimlik numarası belirtilmedi.");
        }

        LogProductId(productId);

        IReadOnlyList<InventoryDto> inventory =
            await _inventoryService.GetProductInventoryAsync(
                productId,
                _options.MaxInventoryLocationResults,
                cancellationToken);

        InventoryAgentResult[] results = inventory
            .Take(_options.MaxInventoryLocationResults)
            .Select(ToAgentResult)
            .ToArray();

        return results.Length == 0
            ? ToolExecutionResult.InventoryNotFound()
            : ToolExecutionResult.FromSuccess(results);
    }

    private async Task<ToolExecutionResult> ExecuteGetTotalProductStockAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetPositiveProductId(arguments, out int productId))
        {
            _logger.LogArguments("productId", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir ürün kimlik numarası belirtilmedi.");
        }

        LogProductId(productId);

        long? totalQuantityAvailable =
            await _inventoryService.GetTotalAvailableStockAsync(
                productId,
                cancellationToken);

        return totalQuantityAvailable is null
            ? ToolExecutionResult.InventoryNotFound()
            : ToolExecutionResult.FromSuccess(
                new ProductStockAgentResult(totalQuantityAvailable.Value));
    }

    private void LogProductId(int productId)
    {
        _logger.LogArguments(
            "productId",
            productId.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryGetPositiveProductId(
        JsonElement arguments,
        out int productId)
    {
        productId = 0;

        return TryGetOnlyArgument(arguments, "productId", out JsonElement idElement)
            && idElement.ValueKind == JsonValueKind.Number
            && idElement.TryGetInt32(out productId)
            && productId > 0;
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

    private static InventoryAgentResult ToAgentResult(InventoryDto inventory)
    {
        return new InventoryAgentResult(
            inventory.LocationCode,
            inventory.LocationName,
            inventory.QuantityAvailable);
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
