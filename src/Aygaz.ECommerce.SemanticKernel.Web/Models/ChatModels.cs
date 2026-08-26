namespace Aygaz.ECommerce.SemanticKernel.Web.Models;

public sealed record ChatRequest(string? Message, string? SessionId);

public sealed record ChatResponse(
    string Message,
    string DomainDecision,
    string? Capability,
    string? SelectedAgent,
    long DurationMs,
    string SessionId,
    ChatDebugMetadata? Debug = null);

public sealed record ChatDebugMetadata(
    string? InvokedFunction,
    int GuardrailInferenceCount,
    int? AgentInferenceCount,
    int FunctionInvocationCount,
    bool FastPathUsed);

public sealed record ClearSessionRequest(string? SessionId);

public sealed record ClearSessionResponse(bool Success, string SessionId);

public sealed record ApiErrorResponse(bool Success, string Message, string? ErrorCode = null);
