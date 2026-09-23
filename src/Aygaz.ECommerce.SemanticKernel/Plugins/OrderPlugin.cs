using System.ComponentModel;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Plugins;

public sealed class OrderPlugin
{
    private const string AgentActor = "semantic-kernel-agent";
    private readonly IOrderService _orderService;
    private readonly IOrderOperationService _orderOperationService;
    private readonly int _maxOrderSearchResults;

    public OrderPlugin(
        IOrderService orderService,
        IOrderOperationService orderOperationService,
        int maxOrderSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(orderService);
        ArgumentNullException.ThrowIfNull(orderOperationService);

        if (maxOrderSearchResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOrderSearchResults),
                "MaxOrderSearchResults must be greater than zero.");
        }

        _orderService = orderService;
        _orderOperationService = orderOperationService;
        _maxOrderSearchResults = maxOrderSearchResults;
    }

    [KernelFunction("get_order_by_number")]
    [Description("Use only when the user provided an explicit order number such as AYG-DEMO-1004. Do not use for customer lookup or bulk order listing.")]
    public async Task<OrderAgentResult?> GetOrderByNumberAsync(
        [Description("Exact order number.")] string orderNumber,
        CancellationToken cancellationToken = default)
    {
        string trimmed = orderNumber?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > CustomerOrder.MaximumOrderNumberLength)
        {
            return null;
        }

        OrderDto? order = await _orderService.GetOrderByNumberAsync(trimmed, cancellationToken);
        return order is null ? null : ToResult(order);
    }

    [KernelFunction("get_customer_orders")]
    [Description("Use when the user asks for a specific customer's order history by numeric customer id, such as '1 numaralı müşterinin siparişlerini göster'. Do not use for system-wide all-order listing.")]
    public async Task<IReadOnlyList<OrderAgentResult>> GetCustomerOrdersAsync(
        [Description("Positive numeric customer id from the user request, e.g. 1.")] int customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return [];
        }

        IReadOnlyList<OrderDto> orders = await _orderService.GetCustomerOrdersAsync(
            customerId,
            _maxOrderSearchResults,
            cancellationToken);

        return orders
            .Take(_maxOrderSearchResults)
            .Select(ToResult)
            .ToArray();
    }

    [KernelFunction("get_latest_customer_order")]
    [Description("Use only when the user asks for the latest order of a specific customer id. Prefer get_order_by_number when an explicit order number is available.")]
    public async Task<OrderAgentResult?> GetLatestCustomerOrderAsync(
        [Description("Numeric customer id.")] int customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return null;
        }

        OrderDto? order = await _orderService.GetLatestCustomerOrderAsync(customerId, cancellationToken);
        return order is null ? null : ToResult(order);
    }

    [KernelFunction("cancel_order")]
    [Description("Use only when the user explicitly asks to cancel an order, provides an exact order number, and provides the cancellation reason.")]
    public async Task<string> CancelOrderAsync(
        [Description("Exact order number.")] string orderNumber,
        [Description("Cancellation reason provided by the user.")] string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(reason))
        {
            return "Siparisi iptal etmek icin siparis numarasi ve iptal nedeni gerekli.";
        }

        OrderOperationResultDto result = await _orderOperationService.CancelOrderAsync(
            orderNumber.Trim(),
            reason.Trim(),
            AgentActor,
            cancellationToken);

        return FormatOperationResult(result);
    }

    [KernelFunction("update_order_status")]
    [Description("Use only when the user explicitly asks to update an order status, provides an exact order number, the target status, and the reason.")]
    public async Task<string> UpdateOrderStatusAsync(
        [Description("Exact order number.")] string orderNumber,
        [Description("Target status: Pending, Preparing, Shipped, Delivered, or Cancelled.")] string newStatus,
        [Description("Status update reason provided by the user.")] string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(newStatus) || string.IsNullOrWhiteSpace(reason))
        {
            return "Durum guncellemek icin siparis numarasi, yeni durum ve islem nedeni gerekli.";
        }

        if (!Enum.TryParse(newStatus.Trim(), true, out OrderStatus parsedStatus))
        {
            return "Gecersiz siparis durumu. Gecerli durumlar: Pending, Preparing, Shipped, Delivered, Cancelled.";
        }

        OrderOperationResultDto result = await _orderOperationService.UpdateOrderStatusAsync(
            orderNumber.Trim(),
            parsedStatus,
            reason.Trim(),
            AgentActor,
            cancellationToken);

        return FormatOperationResult(result);
    }

    [KernelFunction("get_order_audit_logs")]
    [Description("Use when the user asks for audit history, operation history, or change log of an exact order number.")]
    public async Task<IReadOnlyList<OrderAuditLogDto>> GetOrderAuditLogsAsync(
        [Description("Exact order number.")] string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return [];
        }

        return await _orderOperationService.GetOrderAuditLogsAsync(
            orderNumber.Trim(),
            _maxOrderSearchResults,
            cancellationToken);
    }

    private static OrderAgentResult ToResult(OrderDto order)
    {
        return new OrderAgentResult(
            order.Id,
            order.OrderNumber,
            order.OrderDate,
            ToTurkishStatus(order.Status),
            order.TotalAmount);
    }

    private static string ToTurkishStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => "Bekliyor",
            OrderStatus.Preparing => "Hazırlanıyor",
            OrderStatus.Shipped => "Kargoya verildi",
            OrderStatus.Delivered => "Teslim edildi",
            OrderStatus.Cancelled => "İptal edildi",
            _ => "Bilinmiyor"
        };
    }

    private static string FormatOperationResult(OrderOperationResultDto result)
    {
        if (!result.Success || result.Order is null || result.NewStatus is null)
        {
            return result.Message;
        }

        string auditSuffix = string.IsNullOrWhiteSpace(result.AuditLogId)
            ? string.Empty
            : $" Audit kaydi: {result.AuditLogId}.";
        return $"{result.Order.OrderNumber} siparisinin durumu {ToTurkishStatus(result.NewStatus.Value)} olarak guncellendi.{auditSuffix}";
    }
}
