using System.Globalization;
using System.Text;
using Aygaz.ECommerce.Agent.Models.Agent;

namespace Aygaz.ECommerce.SemanticKernel.Formatting;

public enum OrderLookupDetail
{
    Summary,
    Status,
    Full,
    OrderNumber,
    List
}

public static class OrderLookupResponseFormatter
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool TryFormatExactLookup(
        string functionName,
        object? value,
        string? userMessage,
        out string text)
    {
        text = string.Empty;
        OrderLookupDetail detail = ResolveDetail(userMessage);

        if (functionName is "get_order_by_number" or "get_latest_customer_order")
        {
            if (value is null)
            {
                text = "Sipariş bulunamadı.";
                return true;
            }

            if (TryGetSingleOrder(value, out OrderAgentResult? order) && order is not null)
            {
                text = FormatOrder(order, detail);
                return true;
            }

            return false;
        }

        if (functionName == "get_customer_orders"
            && TryGetOrderResults(value, out IReadOnlyList<OrderAgentResult> orders))
        {
            text = FormatOrderList(orders, userMessage);
            return true;
        }

        return false;
    }

    public static OrderLookupDetail ResolveDetail(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return OrderLookupDetail.Summary;
        }

        string normalized = userMessage.ToLower(Turkish);
        bool status = ContainsStatusIntent(normalized);
        bool full = ContainsFullInfoIntent(normalized);
        bool orderNumber = ContainsOrderNumberIntent(normalized);

        if (orderNumber && !status && !full)
        {
            return OrderLookupDetail.OrderNumber;
        }

        if (status && !full && !orderNumber)
        {
            return OrderLookupDetail.Status;
        }

        if (full && !orderNumber)
        {
            return OrderLookupDetail.Full;
        }

        if (status)
        {
            return OrderLookupDetail.Status;
        }

        return OrderLookupDetail.Summary;
    }

    private static bool ContainsStatusIntent(string normalized)
    {
        return normalized.Contains("durumu", StringComparison.Ordinal)
            || normalized.Contains("durum", StringComparison.Ordinal)
            || normalized.Contains("status", StringComparison.Ordinal);
    }

    private static bool ContainsFullInfoIntent(string normalized)
    {
        return normalized.Contains("bilgilerini", StringComparison.Ordinal)
            || normalized.Contains("bilgileri getir", StringComparison.Ordinal)
            || normalized.Contains("bilgileri göster", StringComparison.Ordinal)
            || normalized.Contains("bilgileri goster", StringComparison.Ordinal)
            || normalized.Contains("tüm bilgi", StringComparison.Ordinal)
            || normalized.Contains("tum bilgi", StringComparison.Ordinal)
            || normalized.Contains("sipariş bilgiler", StringComparison.Ordinal)
            || normalized.Contains("siparis bilgiler", StringComparison.Ordinal);
    }

    private static bool ContainsOrderNumberIntent(string normalized)
    {
        return normalized.Contains("sipariş numarası", StringComparison.Ordinal)
            || normalized.Contains("siparis numarasi", StringComparison.Ordinal)
            || normalized.Contains("numarası neydi", StringComparison.Ordinal)
            || normalized.Contains("numarasi neydi", StringComparison.Ordinal);
    }

    public static string FormatOrder(OrderAgentResult order, OrderLookupDetail detail)
    {
        return detail switch
        {
            OrderLookupDetail.Status or OrderLookupDetail.Summary =>
                $"Siparişin durumu {order.Status}.",
            OrderLookupDetail.OrderNumber =>
                $"Sipariş numarası {order.OrderNumber}.",
            OrderLookupDetail.Full => FormatFullSummary(order),
            _ => $"Siparişin durumu {order.Status}."
        };
    }

    private static string FormatFullSummary(OrderAgentResult order)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Sipariş Bilgileri");
        builder.AppendLine($"Sipariş No: {order.OrderNumber}");
        builder.AppendLine($"Tarih: {order.OrderDate.ToString("dd.MM.yyyy", Turkish)}");
        builder.AppendLine($"Durum: {order.Status}");
        builder.Append($"Toplam Tutar: {order.TotalAmount.ToString("N2", Turkish)} TL");
        return builder.ToString();
    }

    private static string FormatOrderList(IReadOnlyList<OrderAgentResult> orders, string? userMessage)
    {
        OrderAgentResult[] list = orders.Take(5).ToArray();
        if (list.Length == 0)
        {
            return "Bu müşteri için sipariş bulunamadı.";
        }

        int? customerId = ExtractCustomerId(userMessage);
        var builder = new StringBuilder();
        builder.AppendLine(customerId is null
            ? "Müşteri siparişleri:"
            : $"{customerId} numaralı müşterinin siparişleri:");
        foreach (OrderAgentResult order in list)
        {
            builder.AppendLine($"- {order.OrderNumber} — {order.Status}");
        }

        return builder.ToString().TrimEnd();
    }

    private static bool TryGetSingleOrder(object value, out OrderAgentResult? order)
    {
        order = value as OrderAgentResult;
        return order is not null;
    }

    internal static bool TryGetOrderResults(object? value, out IReadOnlyList<OrderAgentResult> orders)
    {
        orders = [];

        if (value is null)
        {
            return true;
        }

        if (value is OrderAgentResult single)
        {
            orders = [single];
            return true;
        }

        if (value is IEnumerable<OrderAgentResult> typed)
        {
            orders = typed.ToArray();
            return true;
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var list = new List<OrderAgentResult>();
            foreach (object? item in enumerable)
            {
                if (item is OrderAgentResult order)
                {
                    list.Add(order);
                }
            }

            orders = list;
            return true;
        }

        return false;
    }

    private static int? ExtractCustomerId(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string normalized = message.ToLower(Turkish);
        string[] tokens = normalized.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = 0; i < tokens.Length; i++)
        {
            if ((tokens[i] is "numaralı" or "numarali" or "id'si" or "idsi" or "id")
                && i > 0
                && int.TryParse(tokens[i - 1], out int customerId)
                && customerId > 0)
            {
                return customerId;
            }
        }

        return null;
    }
}
