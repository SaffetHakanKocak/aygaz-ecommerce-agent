namespace Aygaz.ECommerce.Agent.Agent;

public sealed record GuardedAgentResponse(
    string Message,
    string Scope,
    bool Success);
