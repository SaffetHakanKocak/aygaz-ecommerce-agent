using Aygaz.ECommerce.SemanticKernel.Capabilities;
using Aygaz.ECommerce.SemanticKernel.Guardrails;

namespace Aygaz.ECommerce.SemanticKernel.Services;

public sealed record SemanticKernelChatResult(
    string Message,
    DomainDecision DomainDecision,
    AygazCapability Capability,
    string? SelectedAgent,
    long DurationMs,
    bool BusinessAgentInvoked,
    bool BusinessFunctionInvoked,
    int GuardrailInferenceCount,
    int? AgentInferenceCount,
    int FunctionInvocationCount,
    string? InvokedFunctionName,
    bool FastPathUsed);
