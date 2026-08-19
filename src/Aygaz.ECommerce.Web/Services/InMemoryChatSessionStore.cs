using System.Collections.Concurrent;
using Aygaz.ECommerce.Agent.Agent;

namespace Aygaz.ECommerce.Web.Services;

public sealed class InMemoryChatSessionStore : IChatSessionStore, IDisposable
{
    private sealed class SessionEntry(IServiceScope scope) : IDisposable
    {
        public IServiceScope Scope { get; } = scope;

        public IGuardedAgentService GuardedAgent { get; } =
            scope.ServiceProvider.GetRequiredService<IGuardedAgentService>();

        public void Dispose()
        {
            Scope.Dispose();
        }
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);

    public InMemoryChatSessionStore(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<ChatSessionHandle> GetOrCreateAsync(
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string resolvedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString("N")
            : sessionId.Trim();

        SessionEntry entry = _sessions.GetOrAdd(
            resolvedSessionId,
            CreateSessionEntry);

        return Task.FromResult(
            new ChatSessionHandle(resolvedSessionId, entry.GuardedAgent));
    }

    public bool ClearSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_sessions.TryRemove(sessionId.Trim(), out SessionEntry? entry))
        {
            return false;
        }

        entry.Dispose();
        return true;
    }

    public void Dispose()
    {
        foreach (string sessionId in _sessions.Keys.ToArray())
        {
            ClearSession(sessionId);
        }
    }

    private SessionEntry CreateSessionEntry(string sessionId)
    {
        return new SessionEntry(_scopeFactory.CreateScope());
    }
}
