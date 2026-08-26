#pragma warning disable SKEXP0001

using System.Globalization;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public sealed class OperationResultTerminationFilter : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        await next(context);

        object? value = context.Result?.GetValue<object>();
        if (!TryFormat(context.Function.Name, value, out string text))
        {
            return;
        }

        context.Result = new FunctionResult(context.Function, text);
        context.Terminate = true;
    }

    private static bool TryFormat(string functionName, object? value, out string text)
    {
        text = string.Empty;
        switch (functionName)
        {
            case "get_product_by_sku":
                text = value is ProductAgentResult product
                    ? FormatProduct(product)
                    : "Urun bulunamadi.";
                return true;
            case "search_products":
                return TryFormatProducts(value, out text);
            case "get_total_product_stock":
                text = value is ProductStockAgentResult stock
                    ? $"Toplam kullanilabilir stok: {stock.TotalQuantityAvailable} adet."
                    : "Stok bilgisi bulunamadi.";
                return true;
            case "get_product_inventory":
                return TryFormatInventory(value, out text);
            case "get_sales_summary":
                text = value is SalesSummaryAgentResult sales
                    ? $"Satis ozeti: toplam gelir {FormatMoney(sales.TotalRevenue, sales.CurrencyCode)}, siparis sayisi {sales.OrderCount}, satilan urun adedi {sales.ItemsSold}, ortalama siparis tutari {FormatMoney(sales.AverageOrderValue, sales.CurrencyCode)}."
                    : "Satis ozeti bulunamadi.";
                return true;
            case "get_top_selling_products":
                return TryFormatTopProducts(value, out text);
            case "get_customer_purchase_summary":
                text = value is CustomerPurchaseSummaryAgentResult customer
                    ? $"Musteri alim ozeti: {customer.OrderCount} siparis, {customer.ItemsPurchased} urun, toplam harcama {FormatMoney(customer.TotalSpent, customer.CurrencyCode)}."
                    : "Musteri alim ozeti bulunamadi.";
                return true;
            case "search_support_policy":
                return TryFormatPolicy(value, out text);
            default:
                return false;
        }
    }

    private static bool TryFormatProducts(object? value, out string text)
    {
        ProductAgentResult[] products = AsArray<ProductAgentResult>(value);
        if (products.Length == 0)
        {
            text = "Urun bulunamadi.";
            return true;
        }

        text = products.Length == 1
            ? FormatProduct(products[0])
            : "Urunler:\n" + string.Join("\n", products.Select(product =>
                $"- {product.Sku}: {product.Name} ({product.Category}) - {FormatMoney(product.UnitPrice, "TRY")}"));
        return true;
    }

    private static bool TryFormatInventory(object? value, out string text)
    {
        InventoryAgentResult[] inventory = AsArray<InventoryAgentResult>(value);
        if (inventory.Length == 0)
        {
            text = "Stok bilgisi bulunamadi.";
            return true;
        }

        text = "Lokasyon bazli stok:\n" + string.Join("\n", inventory.Select(item =>
            $"- {item.LocationCode} / {item.LocationName}: {item.QuantityAvailable} adet"));
        return true;
    }

    private static bool TryFormatTopProducts(object? value, out string text)
    {
        if (value is not TopSellingProductsAgentResult result || result.Products.Count == 0)
        {
            text = "En cok satan urun bulunamadi.";
            return true;
        }

        text = "En cok satan urunler:\n" + string.Join("\n", result.Products.Select(product =>
            $"- {product.Sku}: {product.ProductName}, {product.QuantitySold} adet, {FormatMoney(product.Revenue, result.CurrencyCode)}"));
        return true;
    }

    private static bool TryFormatPolicy(object? value, out string text)
    {
        PolicySearchResult[] results = AsArray<PolicySearchResult>(value);
        if (results.Length == 0)
        {
            text = "Ilgili Aygaz destek politikasi bilgisi bulunamadi.";
            return true;
        }

        text = "Ilgili politika bilgisi:\n" + string.Join("\n", results.Take(3).Select(result =>
            $"- {result.DocumentName}: {TrimText(result.Text, 240)}"));
        return true;
    }

    private static string FormatProduct(ProductAgentResult product)
    {
        string status = product.IsActive ? "aktif" : "pasif";
        return $"Urun: {product.Name}\nSKU: {product.Sku}\nKategori: {product.Category}\nBirim fiyat: {FormatMoney(product.UnitPrice, "TRY")}\nDurum: {status}\nUrun ID: {product.Id}";
    }

    private static T[] AsArray<T>(object? value)
    {
        return value switch
        {
            null => [],
            T single => [single],
            IEnumerable<T> many => many.ToArray(),
            _ => []
        };
    }

    private static string FormatMoney(decimal amount, string currencyCode)
    {
        return $"{amount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} {currencyCode}";
    }

    private static string TrimText(string value, int maxLength)
    {
        string normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }
}
