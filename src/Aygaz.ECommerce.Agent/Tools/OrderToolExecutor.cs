using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class OrderToolExecutor : IAgentToolModule
{
    public const string GetOrderByNumberToolName = "get_order_by_number";
    public const string GetCustomerOrdersToolName = "get_customer_orders";
    public const string GetLatestCustomerOrderToolName =
        "get_latest_customer_order";

    private static readonly IReadOnlyList<OllamaToolDefinition> Definitions =
        Array.AsReadOnly(new OllamaToolDefinition[]
        {
            CreateToolDefinition(
                GetOrderByNumberToolName,
                "Sipariş numarasına göre tek bir sentetik Aygaz siparişini getirir.",
                "orderNumber",
                "string",
                "Aranacak siparişin numarası."),
            CreateToolDefinition(
                GetCustomerOrdersToolName,
                "Belirli bir müşteri kimliği için en güncel siparişleri sınırlı sayıda getirir. "
                    + "Müşteri kimliği bilinmiyorsa önce uygun müşteri arama tool'unu kullan; "
                    + "ID bulununca ara metin üretmeden hemen bu tool'u çağır.",
                "customerId",
                "integer",
                "Siparişleri aranacak müşterinin pozitif kimlik numarası."),
            CreateToolDefinition(
                GetLatestCustomerOrderToolName,
                "Belirli bir müşteri kimliğinin en son siparişini getirir. "
                    + "Müşteri kimliği bilinmiyorsa önce uygun müşteri arama tool'unu kullan; "
                    + "ID bulununca ara metin üretmeden hemen bu tool'u çağır.",
                "customerId",
                "integer",
                "Son siparişi aranacak müşterinin pozitif kimlik numarası.")
        });

    private readonly IOrderService _orderService;
    private readonly AgentOptions _options;
    private readonly IToolCallLogger _logger;

    public OrderToolExecutor(
        IOrderService orderService,
        IOptions<AgentOptions> options,
        IToolCallLogger logger)
    {
        ArgumentNullException.ThrowIfNull(orderService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (options.Value.MaxOrderSearchResults is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Agent:MaxOrderSearchResults 1 ile 20 arasında olmalıdır.");
        }

        _orderService = orderService;
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
            GetOrderByNumberToolName =>
                await ExecuteGetByNumberAsync(arguments, cancellationToken),
            GetCustomerOrdersToolName =>
                await ExecuteGetCustomerOrdersAsync(arguments, cancellationToken),
            GetLatestCustomerOrderToolName =>
                await ExecuteGetLatestCustomerOrderAsync(arguments, cancellationToken),
            _ => ToolExecutionResult.Rejected("İstenen tool kullanılamıyor.")
        };

        _logger.LogResult(result.Status);
        return result;
    }

    private async Task<ToolExecutionResult> ExecuteGetByNumberAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetOnlyArgument(arguments, "orderNumber", out JsonElement numberElement)
            || numberElement.ValueKind != JsonValueKind.String)
        {
            _logger.LogArguments("orderNumber", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir sipariş numarası belirtilmedi.");
        }

        string orderNumber = numberElement.GetString()?.Trim() ?? string.Empty;
        _logger.LogArguments("orderNumber", orderNumber);

        if (orderNumber.Length is 0 or > CustomerOrder.MaximumOrderNumberLength)
        {
            return ToolExecutionResult.Rejected(
                "Geçerli bir sipariş numarası belirtilmedi.");
        }

        OrderDto? order = await _orderService.GetOrderByNumberAsync(
            orderNumber,
            cancellationToken);

        return order is null
            ? ToolExecutionResult.OrderNotFound()
            : ToolExecutionResult.FromSuccess(ToAgentResult(order));
    }

    private async Task<ToolExecutionResult> ExecuteGetCustomerOrdersAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetPositiveCustomerId(arguments, out int customerId))
        {
            _logger.LogArguments("customerId", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir müşteri kimlik numarası belirtilmedi.");
        }

        _logger.LogArguments(
            "customerId",
            customerId.ToString(CultureInfo.InvariantCulture));

        IReadOnlyList<OrderDto> orders =
            await _orderService.GetCustomerOrdersAsync(
                customerId,
                _options.MaxOrderSearchResults,
                cancellationToken);

        OrderAgentResult[] results = orders
            .Take(_options.MaxOrderSearchResults)
            .Select(ToAgentResult)
            .ToArray();

        return results.Length == 0
            ? ToolExecutionResult.OrderNotFound()
            : ToolExecutionResult.FromSuccess(results);
    }

    private async Task<ToolExecutionResult> ExecuteGetLatestCustomerOrderAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetPositiveCustomerId(arguments, out int customerId))
        {
            _logger.LogArguments("customerId", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir müşteri kimlik numarası belirtilmedi.");
        }

        _logger.LogArguments(
            "customerId",
            customerId.ToString(CultureInfo.InvariantCulture));

        OrderDto? order = await _orderService.GetLatestCustomerOrderAsync(
            customerId,
            cancellationToken);

        return order is null
            ? ToolExecutionResult.OrderNotFound()
            : ToolExecutionResult.FromSuccess(ToAgentResult(order));
    }

    private static bool TryGetPositiveCustomerId(
        JsonElement arguments,
        out int customerId)
    {
        customerId = 0;

        return TryGetOnlyArgument(arguments, "customerId", out JsonElement idElement)
            && idElement.ValueKind == JsonValueKind.Number
            && idElement.TryGetInt32(out customerId)
            && customerId > 0;
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

    private static OrderAgentResult ToAgentResult(OrderDto order)
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
