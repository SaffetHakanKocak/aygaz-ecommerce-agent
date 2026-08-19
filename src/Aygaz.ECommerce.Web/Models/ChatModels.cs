namespace Aygaz.ECommerce.Web.Models;

public sealed record ChatRequest(string? Message, string? SessionId);

public sealed record ChatResponse(
    bool Success,
    string Message,
    string Scope,
    string SessionId);

public sealed record ClearSessionRequest(string? SessionId);

public sealed record ClearSessionResponse(bool Success, string SessionId);

public sealed record ApiErrorResponse(bool Success, string Message);
