using System.Text.Json;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Tools;

namespace Aygaz.ECommerce.Agent.Agent;

internal static class SimpleToolResultFormatter
{
    private static readonly HashSet<string> IntermediateTools =
        new(StringComparer.Ordinal)
        {
            CustomerToolExecutor.GetCustomerByEmailToolName,
            CustomerToolExecutor.GetCustomerByIdToolName,
            CustomerToolExecutor.SearchCustomersByNameToolName,
            ProductToolExecutor.GetProductBySkuToolName,
            ProductToolExecutor.SearchProductsToolName,
            OrderToolExecutor.GetCustomerOrdersToolName,
            InventoryToolExecutor.GetProductInventoryToolName,
            DocumentToolExecutor.SearchDocumentsToolName
        };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static bool CanUseFastPath(string toolName)
    {
        return !IntermediateTools.Contains(toolName);
    }

    public static bool TryFormat(
        string toolName,
        ToolExecutionResult result,
        out string formattedAnswer)
    {
        formattedAnswer = string.Empty;

        if (result.Status == ToolExecutionStatus.NotFound)
        {
            formattedAnswer = TryGetNotFoundMessage(result.Content)
                ?? "İstenen bilgi bulunamadı.";
            return true;
        }

        if (result.Status != ToolExecutionStatus.Success)
        {
            return false;
        }

        return toolName switch
        {
            CustomerToolExecutor.GetCustomerByEmailToolName
                or CustomerToolExecutor.GetCustomerByIdToolName
                => TryFormatSingleCustomer(result.Content, out formattedAnswer),
            OrderToolExecutor.GetOrderByNumberToolName
                or OrderToolExecutor.GetLatestCustomerOrderToolName
                => TryFormatSingleOrder(result.Content, out formattedAnswer),
            InventoryToolExecutor.GetTotalProductStockToolName
                => TryFormatTotalStock(result.Content, out formattedAnswer),
            _ => false
        };
    }

    private static bool TryFormatSingleCustomer(string content, out string formattedAnswer)
    {
        formattedAnswer = string.Empty;

        if (!TryGetDataElement(content, out JsonElement data)
            || !TryDeserialize(data, out CustomerAgentResult? customer)
            || customer is null)
        {
            return false;
        }

        formattedAnswer = $"Müşteri: {customer.FirstName} {customer.LastName}.";
        return true;
    }

    private static bool TryFormatSingleOrder(string content, out string formattedAnswer)
    {
        formattedAnswer = string.Empty;

        if (!TryGetDataElement(content, out JsonElement data)
            || !TryDeserialize(data, out OrderAgentResult? order)
            || order is null)
        {
            return false;
        }

        formattedAnswer =
            $"Sipariş {order.OrderNumber} durumu: {order.Status}.";
        return true;
    }

    private static bool TryFormatTotalStock(string content, out string formattedAnswer)
    {
        formattedAnswer = string.Empty;

        if (!TryGetDataElement(content, out JsonElement data)
            || !TryDeserialize(data, out ProductStockAgentResult? stock)
            || stock is null)
        {
            return false;
        }

        formattedAnswer = stock.TotalQuantityAvailable > 0
            ? $"Toplam kullanılabilir stok miktarı {stock.TotalQuantityAvailable} adettir."
            : "Stokta bulunmamaktadır.";

        return true;
    }

    private static string? TryGetNotFoundMessage(string content)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("message", out JsonElement messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static bool TryGetDataElement(string content, out JsonElement data)
    {
        data = default;

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("found", out JsonElement foundElement)
                && foundElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (root.TryGetProperty("data", out JsonElement dataElement))
            {
                data = dataElement.Clone();
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static bool TryDeserialize<T>(JsonElement data, out T? value)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(data, JsonOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }
}
