using System.ComponentModel;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Plugins;

public sealed class OrderPlugin
{
    private readonly IOrderService _orderService;
    private readonly int _maxOrderSearchResults;

    public OrderPlugin(IOrderService orderService, int maxOrderSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(orderService);

        if (maxOrderSearchResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOrderSearchResults),
                "MaxOrderSearchResults must be greater than zero.");
        }

        _orderService = orderService;
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
}
