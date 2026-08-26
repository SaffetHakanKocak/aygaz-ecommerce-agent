using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Aygaz.ECommerce.SemanticKernel.Formatting;
using Aygaz.ECommerce.SemanticKernel.Guardrails;
using Aygaz.ECommerce.SemanticKernel.Capabilities;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Services;

public sealed class SemanticKernelChatService : ISemanticKernelChatService
{
    private static readonly Regex SkuPattern = new(
        @"\bAYG-[A-Z0-9]+(?:-[A-Z0-9]+)+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex OrderNumberPattern = new(
        @"\bAYG-(?:DEMO-)?\d{4}-\d{4}\b|\bAYG-DEMO-\d+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly SemanticKernelAgentHost _host;

    public SemanticKernelChatService(SemanticKernelAgentHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public async Task<SemanticKernelChatResult> ProcessAsync(
        string userMessage,
        ChatHistory? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        var stopwatch = Stopwatch.StartNew();
        string normalizedMessage = userMessage.Trim();

        if (GreetingFastPath.IsGreeting(normalizedMessage))
        {
            stopwatch.Stop();
            return new SemanticKernelChatResult(
                GreetingFastPath.GreetingResponse,
                DomainDecision.Allowed,
                AygazCapability.Unknown,
                SelectedAgent: null,
                DurationMs: stopwatch.ElapsedMilliseconds,
                BusinessAgentInvoked: false,
                BusinessFunctionInvoked: false,
                GuardrailInferenceCount: 0,
                AgentInferenceCount: null,
                FunctionInvocationCount: 0,
                InvokedFunctionName: null,
                FastPathUsed: true);
        }

        // Deterministic in-domain company-info / unsupported capabilities — no LLM needed.
        if (AygazCompanyInfoResolver.TryResolve(normalizedMessage, out AygazCapability companyCapability)
            && companyCapability == AygazCapability.Unknown)
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                UnsupportedCapabilityResponse,
                new AygazDomainClassificationResult(
                    DomainDecision.Allowed,
                    companyCapability,
                    "aygaz_company_info_fast_path",
                    stopwatch.Elapsed,
                    0),
                companyCapability,
                stopwatch.ElapsedMilliseconds);
        }

        // Global bulk must not consume LLM or be rewritten by history.
        if (OrderBulkRequestDetector.IsGlobalBulk(normalizedMessage))
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                BulkOrderListUnsupportedResponse,
                new AygazDomainClassificationResult(
                    DomainDecision.Allowed,
                    AygazCapability.Order,
                    "global_order_bulk_fast_path",
                    stopwatch.Elapsed,
                    0),
                AygazCapability.Order,
                stopwatch.ElapsedMilliseconds);
        }

        if (TryExtractOrderNumber(normalizedMessage, out string orderNumber)
            && await TryHandleOrderFastPathAsync(
                normalizedMessage,
                orderNumber,
                stopwatch,
                cancellationToken) is { } orderFastPathResult)
        {
            return orderFastPathResult;
        }

        AygazDomainClassificationResult guardrail = await _host.Guardrail.EvaluateAsync(
            normalizedMessage,
            conversationHistory,
            cancellationToken);

        guardrail = ApplyCompanyInfoOverride(guardrail, normalizedMessage);
        guardrail = ApplyExplicitIntentOverride(guardrail, normalizedMessage);
        guardrail = ApplyFallbackWhenNeeded(guardrail, normalizedMessage, conversationHistory);

        if (guardrail.Decision == DomainDecision.OutOfScope)
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                DomainGuardedQueryExecutor.OutOfScopeResponse,
                guardrail,
                guardrail.Capability,
                stopwatch.ElapsedMilliseconds);
        }

        if (guardrail.Decision != DomainDecision.Allowed)
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                DomainGuardedQueryExecutor.AmbiguousResponse,
                guardrail,
                guardrail.Capability,
                stopwatch.ElapsedMilliseconds);
        }

        AygazCapability capability = guardrail.Capability;

        // Global bulk is never rewritten by prior customer/order history.
        if (OrderBulkRequestDetector.IsGlobalBulk(normalizedMessage))
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                BulkOrderListUnsupportedResponse,
                guardrail with { Capability = AygazCapability.Order },
                AygazCapability.Order,
                stopwatch.ElapsedMilliseconds);
        }

        if (capability == AygazCapability.Customer
            && IsBulkCustomerListRequest(normalizedMessage)
            && !IsCityScopedCustomerRequest(normalizedMessage))
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                BulkCustomerListUnsupportedResponse,
                guardrail,
                capability,
                stopwatch.ElapsedMilliseconds);
        }

        if (capability == AygazCapability.Order && IsBulkOrderListRequest(normalizedMessage))
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                BulkOrderListUnsupportedResponse,
                guardrail,
                capability,
                stopwatch.ElapsedMilliseconds);
        }

        if (capability == AygazCapability.Unknown)
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                UnsupportedCapabilityResponse,
                guardrail,
                capability,
                stopwatch.ElapsedMilliseconds);
        }

        ChatCompletionAgent? agent = ResolveAgent(capability, normalizedMessage);
        if (agent is null)
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                UnsupportedCapabilityResponse,
                guardrail,
                capability,
                stopwatch.ElapsedMilliseconds);
        }

        if (capability == AygazCapability.ProductInventory
            && TryExtractSku(normalizedMessage, out string sku)
            && await TryHandleProductInventoryFastPathAsync(
                normalizedMessage,
                sku,
                agent.Name ?? string.Empty,
                guardrail,
                stopwatch,
                cancellationToken) is { } fastPathResult)
        {
            return fastPathResult;
        }

        _host.Telemetry.Reset();
        var agentResponse = await _host.Runner.InvokeAsync(
            agent,
            normalizedMessage,
            conversationHistory,
            cancellationToken);

        stopwatch.Stop();
        return new SemanticKernelChatResult(
            agentResponse.Content,
            DomainDecision.Allowed,
            capability,
            agent.Name,
            stopwatch.ElapsedMilliseconds,
            BusinessAgentInvoked: true,
            BusinessFunctionInvoked: _host.Telemetry.FunctionInvocationCount > 0,
            GuardrailInferenceCount: guardrail.InferenceCount,
            AgentInferenceCount: _host.Telemetry.EstimateLlmInferenceCount(),
            FunctionInvocationCount: _host.Telemetry.FunctionInvocationCount,
            InvokedFunctionName: _host.Telemetry.LastFunctionName,
            FastPathUsed: _host.Telemetry.Terminated);
    }

    private ChatCompletionAgent? ResolveAgent(AygazCapability capability, string userMessage)
    {
        string? routeKey = MultiAgentRouteResolver.ResolveRouteKey(capability, userMessage);
        if (routeKey is null)
        {
            return null;
        }

        string? agentName = _host.Router.ResolveAgentName(routeKey);
        return agentName is null ? null : _host.Registry.GetAgent(agentName);
    }

    private async Task<SemanticKernelChatResult?> TryHandleProductInventoryFastPathAsync(
        string userMessage,
        string sku,
        string selectedAgent,
        AygazDomainClassificationResult guardrail,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        ProductDto? product = await _host.ProductService.GetProductBySkuAsync(sku, cancellationToken);
        if (product is null)
        {
            stopwatch.Stop();
            return new SemanticKernelChatResult(
                "Urun bulunamadi.",
                DomainDecision.Allowed,
                AygazCapability.ProductInventory,
                selectedAgent,
                stopwatch.ElapsedMilliseconds,
                BusinessAgentInvoked: true,
                BusinessFunctionInvoked: true,
                GuardrailInferenceCount: guardrail.InferenceCount,
                AgentInferenceCount: 0,
                FunctionInvocationCount: 1,
                InvokedFunctionName: "get_product_by_sku",
                FastPathUsed: true);
        }

        if (selectedAgent == InventoryAgentRegistration.AgentName || LooksLikeInventoryRequest(userMessage))
        {
            long? stock = await _host.InventoryService.GetTotalAvailableStockAsync(product.Id, cancellationToken);
            stopwatch.Stop();
            return new SemanticKernelChatResult(
                stock is null
                    ? "Stok bilgisi bulunamadi."
                    : $"{product.Name} icin toplam kullanilabilir stok: {stock.Value} adet.",
                DomainDecision.Allowed,
                AygazCapability.ProductInventory,
                InventoryAgentRegistration.AgentName,
                stopwatch.ElapsedMilliseconds,
                BusinessAgentInvoked: true,
                BusinessFunctionInvoked: true,
                GuardrailInferenceCount: guardrail.InferenceCount,
                AgentInferenceCount: 0,
                FunctionInvocationCount: 2,
                InvokedFunctionName: "get_total_product_stock",
                FastPathUsed: true);
        }

        stopwatch.Stop();
        return new SemanticKernelChatResult(
            FormatProduct(product),
            DomainDecision.Allowed,
            AygazCapability.ProductInventory,
            ProductAgentRegistration.AgentName,
            stopwatch.ElapsedMilliseconds,
            BusinessAgentInvoked: true,
            BusinessFunctionInvoked: true,
            GuardrailInferenceCount: guardrail.InferenceCount,
            AgentInferenceCount: 0,
            FunctionInvocationCount: 1,
            InvokedFunctionName: "get_product_by_sku",
            FastPathUsed: true);
    }

    private async Task<SemanticKernelChatResult?> TryHandleOrderFastPathAsync(
        string userMessage,
        string orderNumber,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        string normalized = userMessage.ToLower(CultureInfo.GetCultureInfo("tr-TR"));

        if (LooksLikeAuditRequest(normalized))
        {
            IReadOnlyList<OrderAuditLogDto> logs = await _host.OrderOperationService.GetOrderAuditLogsAsync(
                orderNumber,
                5,
                cancellationToken);
            stopwatch.Stop();
            return CreateOrderFastPathResult(
                FormatAuditLogs(orderNumber, logs),
                stopwatch.ElapsedMilliseconds,
                "get_order_audit_logs");
        }

        if (LooksLikeCancelRequest(normalized))
        {
            string? reason = ExtractReason(userMessage);
            if (string.IsNullOrWhiteSpace(reason))
            {
                stopwatch.Stop();
                return CreateOrderFastPathResult(
                    "Siparisi iptal etmek icin iptal nedeni gerekli.",
                    stopwatch.ElapsedMilliseconds,
                    "cancel_order");
            }

            OrderOperationResultDto result = await _host.OrderOperationService.CancelOrderAsync(
                orderNumber,
                reason,
                "semantic-kernel-agent",
                cancellationToken);
            stopwatch.Stop();
            return CreateOrderFastPathResult(
                FormatOrderOperation(result),
                stopwatch.ElapsedMilliseconds,
                "cancel_order");
        }

        if (LooksLikeStatusUpdateRequest(normalized)
            && TryParseRequestedStatus(normalized, out OrderStatus status))
        {
            string? reason = ExtractReason(userMessage);
            if (string.IsNullOrWhiteSpace(reason))
            {
                stopwatch.Stop();
                return CreateOrderFastPathResult(
                    "Durum guncellemek icin islem nedeni gerekli.",
                    stopwatch.ElapsedMilliseconds,
                    "update_order_status");
            }

            OrderOperationResultDto result = await _host.OrderOperationService.UpdateOrderStatusAsync(
                orderNumber,
                status,
                reason,
                "semantic-kernel-agent",
                cancellationToken);
            stopwatch.Stop();
            return CreateOrderFastPathResult(
                FormatOrderOperation(result),
                stopwatch.ElapsedMilliseconds,
                "update_order_status");
        }

        OrderDto? order = await _host.OrderService.GetOrderByNumberAsync(orderNumber, cancellationToken);
        stopwatch.Stop();
        var resultValue = order is null
            ? null
            : new OrderAgentResult(
                order.Id,
                order.OrderNumber,
                order.OrderDate,
                ToTurkishStatus(order.Status),
                order.TotalAmount);
        OrderLookupResponseFormatter.TryFormatExactLookup(
            "get_order_by_number",
            resultValue,
            userMessage,
            out string text);
        return CreateOrderFastPathResult(
            text,
            stopwatch.ElapsedMilliseconds,
            "get_order_by_number");
    }

    private static SemanticKernelChatResult CreateOrderFastPathResult(
        string content,
        long durationMs,
        string invokedFunctionName)
    {
        return new SemanticKernelChatResult(
            content,
            DomainDecision.Allowed,
            AygazCapability.Order,
            OrderAgentRegistration.AgentName,
            durationMs,
            BusinessAgentInvoked: true,
            BusinessFunctionInvoked: true,
            GuardrailInferenceCount: 0,
            AgentInferenceCount: 0,
            FunctionInvocationCount: 1,
            InvokedFunctionName: invokedFunctionName,
            FastPathUsed: true);
    }

    private static bool TryExtractSku(string message, out string sku)
    {
        Match match = SkuPattern.Match(message);
        sku = match.Success ? match.Value.ToUpperInvariant() : string.Empty;
        return match.Success;
    }

    private static bool TryExtractOrderNumber(string message, out string orderNumber)
    {
        Match match = OrderNumberPattern.Match(message);
        orderNumber = match.Success ? match.Value.ToUpperInvariant() : string.Empty;
        return match.Success;
    }

    private static bool LooksLikeInventoryRequest(string message)
    {
        string normalized = message.ToLowerInvariant();
        return normalized.Contains("stok", StringComparison.Ordinal)
               || normalized.Contains("envanter", StringComparison.Ordinal)
               || normalized.Contains("inventory", StringComparison.Ordinal);
    }

    private static string FormatProduct(ProductDto product)
    {
        string status = product.IsActive ? "aktif" : "pasif";
        return "Urun Bilgileri\n"
               + $"SKU: {product.Sku}\n"
               + $"Ad: {product.Name}\n"
               + $"Kategori: {product.Category}\n"
               + $"Birim Fiyat: {product.UnitPrice.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} TRY\n"
               + $"Durum: {status}\n"
               + $"Urun ID: {product.Id}";
    }

    private static bool LooksLikeAuditRequest(string normalized)
    {
        return normalized.Contains("audit", StringComparison.Ordinal)
               || normalized.Contains("kayit", StringComparison.Ordinal)
               || normalized.Contains("kayıt", StringComparison.Ordinal)
               || normalized.Contains("log", StringComparison.Ordinal)
               || normalized.Contains("gecmis", StringComparison.Ordinal)
               || normalized.Contains("geçmiş", StringComparison.Ordinal);
    }

    private static bool LooksLikeCancelRequest(string normalized)
    {
        return normalized.Contains("iptal", StringComparison.Ordinal)
               || normalized.Contains("cancel", StringComparison.Ordinal);
    }

    private static bool LooksLikeStatusUpdateRequest(string normalized)
    {
        return (normalized.Contains("durum", StringComparison.Ordinal)
                || normalized.Contains("status", StringComparison.Ordinal))
               && (normalized.Contains("guncelle", StringComparison.Ordinal)
                   || normalized.Contains("güncelle", StringComparison.Ordinal)
                   || normalized.Contains("degistir", StringComparison.Ordinal)
                   || normalized.Contains("değiştir", StringComparison.Ordinal));
    }

    private static bool TryParseRequestedStatus(string normalized, out OrderStatus status)
    {
        if (normalized.Contains("pending", StringComparison.Ordinal)
            || normalized.Contains("bekliyor", StringComparison.Ordinal))
        {
            status = OrderStatus.Pending;
            return true;
        }

        if (normalized.Contains("preparing", StringComparison.Ordinal)
            || normalized.Contains("hazirlaniyor", StringComparison.Ordinal)
            || normalized.Contains("hazırlanıyor", StringComparison.Ordinal))
        {
            status = OrderStatus.Preparing;
            return true;
        }

        if (normalized.Contains("shipped", StringComparison.Ordinal)
            || normalized.Contains("kargoya", StringComparison.Ordinal))
        {
            status = OrderStatus.Shipped;
            return true;
        }

        if (normalized.Contains("delivered", StringComparison.Ordinal)
            || normalized.Contains("teslim", StringComparison.Ordinal))
        {
            status = OrderStatus.Delivered;
            return true;
        }

        if (normalized.Contains("cancelled", StringComparison.Ordinal)
            || normalized.Contains("canceled", StringComparison.Ordinal)
            || normalized.Contains("iptal", StringComparison.Ordinal))
        {
            status = OrderStatus.Cancelled;
            return true;
        }

        status = default;
        return false;
    }

    private static string? ExtractReason(string userMessage)
    {
        string[] markers = ["neden:", "sebep:", "nedeni:", "sebebi:"];
        foreach (string marker in markers)
        {
            int index = userMessage.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                string reason = userMessage[(index + marker.Length)..].Trim().TrimEnd('.');
                return reason.Length == 0 ? null : reason;
            }
        }

        Match match = Regex.Match(
            userMessage,
            @"(?:nedeniyle|sebebiyle|diye)\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        string value = match.Groups[1].Value.Trim().TrimEnd('.');
        return value.Length == 0 ? null : value;
    }

    private static string FormatOrderOperation(OrderOperationResultDto result)
    {
        if (!result.Success || result.Order is null || result.NewStatus is null)
        {
            return result.Message;
        }

        string audit = string.IsNullOrWhiteSpace(result.AuditLogId)
            ? string.Empty
            : $"\nAudit kaydi: {result.AuditLogId}";
        return $"{result.Order.OrderNumber} siparisinin durumu {ToTurkishStatus(result.NewStatus.Value)} olarak guncellendi.{audit}";
    }

    private static string FormatAuditLogs(string orderNumber, IReadOnlyList<OrderAuditLogDto> logs)
    {
        if (logs.Count == 0)
        {
            return $"{orderNumber} icin audit kaydi bulunamadi.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{orderNumber} audit kayitlari:");
        foreach (OrderAuditLogDto log in logs.Take(5))
        {
            builder.AppendLine($"- {log.CreatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR"))}: {log.Operation}, {ToTurkishStatus(log.PreviousStatus)} -> {ToTurkishStatus(log.NewStatus)}, neden: {log.Reason}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ToTurkishStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => "Bekliyor",
            OrderStatus.Preparing => "Hazirlaniyor",
            OrderStatus.Shipped => "Kargoya verildi",
            OrderStatus.Delivered => "Teslim edildi",
            OrderStatus.Cancelled => "Iptal edildi",
            _ => "Bilinmiyor"
        };
    }

    private static SemanticKernelChatResult CreateBlockedResult(
        string message,
        AygazDomainClassificationResult guardrail,
        AygazCapability capability,
        long durationMs)
    {
        return new SemanticKernelChatResult(
            message,
            guardrail.Decision,
            capability,
            SelectedAgent: null,
            DurationMs: durationMs,
            BusinessAgentInvoked: false,
            BusinessFunctionInvoked: false,
            GuardrailInferenceCount: guardrail.InferenceCount,
            AgentInferenceCount: null,
            FunctionInvocationCount: 0,
            InvokedFunctionName: null,
            FastPathUsed: false);
    }

    internal const string UnsupportedCapabilityResponse =
        "Bu Aygaz özelliği henüz bu demo sürümünde desteklenmiyor.";

    internal const string BulkCustomerListUnsupportedResponse =
        "Toplu müşteri listeleme desteklenmiyor. Belirli bir müşteriyi adı, müşteri numarası veya e-posta adresiyle arayabilirsiniz.";

    internal const string BulkOrderListUnsupportedResponse =
        "Toplu sipariş listeleme desteklenmiyor. Belirli bir sipariş numarası veya müşteri numarasıyla sorgulama yapabilirsiniz.";

    private static bool IsBulkCustomerListRequest(string message)
    {
        if (CustomerFollowUpResolver.IsFollowUpPhrase(message))
        {
            return false;
        }

        string normalized = message.ToLowerInvariant();
        return normalized.Contains("müşterileri getir", StringComparison.Ordinal)
               || normalized.Contains("musterileri getir", StringComparison.Ordinal)
               || normalized.Contains("tüm müşterileri", StringComparison.Ordinal)
               || normalized.Contains("tum musterileri", StringComparison.Ordinal)
               || normalized.Contains("bütün müşterileri", StringComparison.Ordinal)
               || normalized.Contains("butun musterileri", StringComparison.Ordinal)
               || normalized.Contains("tüm müşteri bilgilerini", StringComparison.Ordinal)
               || normalized.Contains("tum musteri bilgilerini", StringComparison.Ordinal)
               || normalized.Contains("müşteri listesi", StringComparison.Ordinal)
               || normalized.Contains("musteri listesi", StringComparison.Ordinal);
    }

    private static bool IsBulkOrderListRequest(string message)
    {
        return OrderBulkRequestDetector.IsGlobalBulk(message);
    }

    private static bool IsCityScopedCustomerRequest(string message)
    {
        string normalized = message.ToLowerInvariant();
        return normalized.Contains("istanbul", StringComparison.Ordinal)
               || normalized.Contains("ankara", StringComparison.Ordinal)
               || normalized.Contains("izmir", StringComparison.Ordinal)
               || normalized.Contains("'da", StringComparison.Ordinal)
               || normalized.Contains("'de", StringComparison.Ordinal)
               || normalized.Contains("daki", StringComparison.Ordinal)
               || normalized.Contains("deki", StringComparison.Ordinal)
               || normalized.Contains("yaşayan müşteri", StringComparison.Ordinal)
               || normalized.Contains("yasayan musteri", StringComparison.Ordinal);
    }

    private static AygazDomainClassificationResult ApplyCompanyInfoOverride(
        AygazDomainClassificationResult result,
        string message)
    {
        if (!AygazCompanyInfoResolver.TryResolve(message, out AygazCapability companyCapability))
        {
            return result;
        }

        return result with
        {
            Decision = DomainDecision.Allowed,
            Capability = companyCapability,
            Reason = "aygaz_company_info_override"
        };
    }

    private static AygazDomainClassificationResult ApplyExplicitIntentOverride(
        AygazDomainClassificationResult result,
        string message)
    {
        string normalized = message.ToLowerInvariant();
        if (IsExplicitOutOfScope(normalized))
        {
            return result;
        }

        // Company-info already applied; do not let Customer markers steal it.
        if (AygazCompanyInfoResolver.TryResolve(message, out _))
        {
            return result;
        }

        if (!ExplicitCapabilityResolver.TryResolve(message, out AygazCapability explicitCapability))
        {
            return result;
        }

        // Company resolver is also invoked inside TryResolve; Unknown/Sales already handled.
        if (explicitCapability is AygazCapability.Unknown
            or AygazCapability.Sales
            or AygazCapability.Policy
            or AygazCapability.ProductInventory)
        {
            return result with
            {
                Decision = DomainDecision.Allowed,
                Capability = explicitCapability,
                Reason = "explicit_capability_override"
            };
        }

        if (result.Decision is DomainDecision.OutOfScope or DomainDecision.Ambiguous)
        {
            return result with
            {
                Decision = DomainDecision.Allowed,
                Capability = explicitCapability,
                Reason = "explicit_intent_override"
            };
        }

        if (result.Capability != explicitCapability)
        {
            return result with
            {
                Capability = explicitCapability,
                Reason = "explicit_intent_override"
            };
        }

        return result;
    }

    private static AygazDomainClassificationResult ApplyFallbackWhenNeeded(
        AygazDomainClassificationResult result,
        string message,
        ChatHistory? history)
    {
        if (AygazCompanyInfoResolver.TryResolve(message, out AygazCapability companyCapability))
        {
            return result with
            {
                Decision = DomainDecision.Allowed,
                Capability = companyCapability,
                Reason = "fallback_aygaz_company_info"
            };
        }

        if (ExplicitCapabilityResolver.TryResolve(message, out _))
        {
            return result;
        }

        if (OrderBulkRequestDetector.IsGlobalBulk(message))
        {
            return result with
            {
                Decision = DomainDecision.Allowed,
                Capability = AygazCapability.Order,
                Reason = "fallback_global_order_bulk"
            };
        }

        if (result.Decision is not (DomainDecision.Ambiguous or DomainDecision.OutOfScope))
        {
            // History must not override an already Allowed non-referential classification
            // into Customer/Order when the current message is an explicit new topic.
            return result;
        }

        string normalized = message.ToLowerInvariant();
        if (IsExplicitOutOfScope(normalized))
        {
            return result with
            {
                Decision = DomainDecision.OutOfScope,
                Capability = AygazCapability.Unknown,
                Reason = "fallback_out_of_scope"
            };
        }

        // History only for truly referential messages.
        if (ReferentialMessageDetector.IsReferentialFollowUp(message))
        {
            if (OrderFollowUpResolver.IsOrderFollowUp(history, message))
            {
                return result with
                {
                    Decision = DomainDecision.Allowed,
                    Capability = AygazCapability.Order,
                    Reason = "fallback_order_follow_up"
                };
            }

            if (CustomerFollowUpResolver.IsCustomerFollowUp(history, message))
            {
                return result with
                {
                    Decision = DomainDecision.Allowed,
                    Capability = AygazCapability.Customer,
                    Reason = "fallback_customer_follow_up"
                };
            }

            // Referential customer-scoped order list with prior customer context.
            if (OrderBulkRequestDetector.IsReferentialCustomerScope(message)
                && CustomerFollowUpResolver.HistoryHasCustomerReference(history))
            {
                return result with
                {
                    Decision = DomainDecision.Allowed,
                    Capability = AygazCapability.Order,
                    Reason = "fallback_referential_customer_orders"
                };
            }
        }

        if (result.Decision == DomainDecision.Ambiguous && LooksLikeOrderIntent(normalized))
        {
            return result with
            {
                Decision = DomainDecision.Allowed,
                Capability = AygazCapability.Order,
                Reason = "fallback_order"
            };
        }

        if (result.Decision == DomainDecision.Ambiguous && LooksLikeCustomerIntent(normalized))
        {
            return result with
            {
                Decision = DomainDecision.Allowed,
                Capability = AygazCapability.Customer,
                Reason = "fallback_customer"
            };
        }

        return result;
    }

    private static bool IsExplicitOutOfScope(string normalized)
    {
        return normalized.Contains("turkcell", StringComparison.Ordinal)
               || normalized.Contains("vodafone", StringComparison.Ordinal)
               || normalized.Contains("arçelik", StringComparison.Ordinal)
               || normalized.Contains("arcelik", StringComparison.Ordinal)
               || normalized.Contains("trendyol", StringComparison.Ordinal)
               || normalized.Contains("amazon", StringComparison.Ordinal)
               || normalized.Contains("bjk", StringComparison.Ordinal)
               || normalized.Contains("başkenti", StringComparison.Ordinal)
               || normalized.Contains("baskenti", StringComparison.Ordinal)
               || normalized.Contains("maçı", StringComparison.Ordinal)
               || normalized.Contains("maci", StringComparison.Ordinal);
    }

    private static bool LooksLikeOrderIntent(string normalized)
    {
        return normalized.Contains("sipariş", StringComparison.Ordinal)
               || normalized.Contains("siparis", StringComparison.Ordinal)
               || normalized.Contains("ayg-demo-", StringComparison.Ordinal);
    }

    private static bool LooksLikeCustomerIntent(string normalized)
    {
        if (normalized.Contains("aygaz", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.Contains("müşteri", StringComparison.Ordinal)
               || normalized.Contains("musteri", StringComparison.Ordinal)
               || normalized.Contains("telefon", StringComparison.Ordinal)
               || normalized.Contains("e-posta", StringComparison.Ordinal)
               || normalized.Contains("eposta", StringComparison.Ordinal)
               || normalized.Contains("email", StringComparison.Ordinal)
               || normalized.Contains("adres", StringComparison.Ordinal)
               || normalized.Contains("nerede yaşıyor", StringComparison.Ordinal)
               || normalized.Contains("nerede yasiyor", StringComparison.Ordinal)
               || normalized.Contains("nerede oturuyor", StringComparison.Ordinal)
               || IsCityScopedCustomerRequest(normalized);
    }
}
