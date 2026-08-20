using System.Text.RegularExpressions;
using Aygaz.ECommerce.SemanticKernel.Services;
using Aygaz.ECommerce.SemanticKernel.Web.Models;

namespace Aygaz.ECommerce.SemanticKernel.Web;

internal static class ResponseSanitizer
{
    private static readonly Regex SecretPattern = new(
        @"sk-[A-Za-z0-9_\-\.*]+|gsk_[A-Za-z0-9_\-\.*]+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string SanitizeError(string message)
    {
        return SecretPattern.Replace(message, "[redacted]");
    }

    public static ChatResponse ToChatResponse(
        SemanticKernelChatResult result,
        string sessionId,
        bool includeDebug)
    {
        return new ChatResponse(
            result.Message,
            result.DomainDecision.ToString(),
            result.Capability.ToString(),
            result.SelectedAgent,
            result.DurationMs,
            sessionId,
            includeDebug
                ? new ChatDebugMetadata(
                    result.InvokedFunctionName,
                    result.GuardrailInferenceCount,
                    result.AgentInferenceCount,
                    result.FunctionInvocationCount,
                    result.FastPathUsed)
                : null);
    }
}
