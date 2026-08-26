using System.Collections.Concurrent;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Web.Services;

public interface IChatSessionStore
{
    string ResolveSessionId(string? sessionId);
    ChatHistory GetHistory(string sessionId);
    void AppendTurn(string sessionId, string userMessage, string assistantMessage);
    bool ClearSession(string sessionId);
    Task<T> ExecuteExclusiveAsync<T>(
        string sessionId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryChatSessionStore : IChatSessionStore
{
    private const int MaxConversationTurns = 6;
    private const int MaxMessages = MaxConversationTurns * 2;
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);

    public string ResolveSessionId(string? sessionId)
    {
        string resolved = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString("N")
            : sessionId.Trim();

        _sessions.TryAdd(resolved, new SessionState());
        return resolved;
    }

    public ChatHistory GetHistory(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        string key = sessionId.Trim();
        SessionState state = _sessions.GetOrAdd(key, _ => new SessionState());

        lock (state.SyncRoot)
        {
            var history = new ChatHistory();
            foreach ((string role, string content) in state.Messages)
            {
                if (role == "user")
                {
                    history.AddUserMessage(content);
                }
                else
                {
                    history.AddAssistantMessage(content);
                }
            }

            return history;
        }
    }

    public void AppendTurn(string sessionId, string userMessage, string assistantMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        string key = sessionId.Trim();
        SessionState state = _sessions.GetOrAdd(key, _ => new SessionState());

        lock (state.SyncRoot)
        {
            state.Messages.Add(("user", userMessage));
            state.Messages.Add(("assistant", assistantMessage));

            while (state.Messages.Count > MaxMessages)
            {
                state.Messages.RemoveAt(0);
            }
        }
    }

    public bool ClearSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _sessions.TryRemove(sessionId.Trim(), out _);
    }

    public async Task<T> ExecuteExclusiveAsync<T>(
        string sessionId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(action);

        string key = sessionId.Trim();
        SessionState state = _sessions.GetOrAdd(key, _ => new SessionState());
        await state.Gate.WaitAsync(cancellationToken);

        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private sealed class SessionState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public object SyncRoot { get; } = new();
        public List<(string Role, string Content)> Messages { get; } = [];
    }
}
