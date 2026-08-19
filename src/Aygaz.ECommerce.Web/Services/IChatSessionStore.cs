using Aygaz.ECommerce.Agent.Agent;

namespace Aygaz.ECommerce.Web.Services;

public interface IChatSessionStore
{
    Task<ChatSessionHandle> GetOrCreateAsync(
        string? sessionId,
        CancellationToken cancellationToken = default);

    bool ClearSession(string sessionId);
}

public sealed record ChatSessionHandle(string SessionId, IGuardedAgentService GuardedAgent);
