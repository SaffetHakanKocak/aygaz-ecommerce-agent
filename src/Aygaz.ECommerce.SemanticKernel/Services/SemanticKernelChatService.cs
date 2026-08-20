using System.Diagnostics;
using Aygaz.ECommerce.SemanticKernel.Agents;
using Aygaz.ECommerce.SemanticKernel.Guardrails;
using Aygaz.ECommerce.SemanticKernel.Capabilities;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Services;

public sealed class SemanticKernelChatService : ISemanticKernelChatService
{
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

        AygazDomainClassificationResult guardrail = await _host.Guardrail.EvaluateAsync(
            normalizedMessage,
            conversationHistory,
            cancellationToken);

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

        if (capability != AygazCapability.Customer)
        {
            stopwatch.Stop();
            return CreateBlockedResult(
                UnsupportedCapabilityResponse,
                guardrail,
                capability,
                stopwatch.ElapsedMilliseconds);
        }

        _host.Telemetry.Reset();
        var agentResponse = await _host.Runner.InvokeAsync(
            _host.CustomerAgent,
            normalizedMessage,
            conversationHistory,
            cancellationToken);

        stopwatch.Stop();
        return new SemanticKernelChatResult(
            agentResponse.Content,
            DomainDecision.Allowed,
            AygazCapability.Customer,
            CustomerAgentRegistration.AgentName,
            stopwatch.ElapsedMilliseconds,
            BusinessAgentInvoked: true,
            BusinessFunctionInvoked: _host.Telemetry.FunctionInvocationCount > 0,
            GuardrailInferenceCount: guardrail.InferenceCount,
            AgentInferenceCount: _host.Telemetry.EstimateLlmInferenceCount(),
            FunctionInvocationCount: _host.Telemetry.FunctionInvocationCount,
            InvokedFunctionName: _host.Telemetry.LastFunctionName,
            FastPathUsed: _host.Telemetry.Terminated);
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

    private static bool IsBulkCustomerListRequest(string message)
    {
        // Follow-up "tüm bilgilerini getir" is not a bulk listing request.
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
               || normalized.Contains("müşteri listesi", StringComparison.Ordinal)
               || normalized.Contains("musteri listesi", StringComparison.Ordinal);
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

    private static AygazDomainClassificationResult ApplyFallbackWhenNeeded(
        AygazDomainClassificationResult result,
        string message,
        ChatHistory? history)
    {
        if (result.Decision is not (DomainDecision.Ambiguous or DomainDecision.OutOfScope))
        {
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

        // Pronoun / omitted-identity follow-ups with prior customer in session history.
        if (CustomerFollowUpResolver.IsCustomerFollowUp(history, message))
        {
            return result with
            {
                Decision = DomainDecision.Allowed,
                Capability = AygazCapability.Customer,
                Reason = "fallback_customer_follow_up"
            };
        }

        // Ambiguous-only: standalone customer-looking phrases without history.
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
               || normalized.Contains("bjk", StringComparison.Ordinal)
               || normalized.Contains("başkenti", StringComparison.Ordinal)
               || normalized.Contains("baskenti", StringComparison.Ordinal)
               || normalized.Contains("maçı", StringComparison.Ordinal)
               || normalized.Contains("maci", StringComparison.Ordinal);
    }

    private static bool LooksLikeCustomerIntent(string normalized)
    {
        return normalized.Contains("müşteri", StringComparison.Ordinal)
               || normalized.Contains("musteri", StringComparison.Ordinal)
               || normalized.Contains("bilgi", StringComparison.Ordinal)
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
