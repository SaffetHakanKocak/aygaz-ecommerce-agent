using System.Collections.Concurrent;

namespace Aygaz.AgentFramework.Routing;

public sealed class AgentRouter : IAgentRouter
{
    private readonly ConcurrentDictionary<string, string> _routes = new(StringComparer.OrdinalIgnoreCase);

    public void MapRoute(string routeKey, string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        _routes[routeKey] = agentName;
    }

    public string? ResolveAgentName(string routeKey)
    {
        return _routes.TryGetValue(routeKey, out var name) ? name : null;
    }

    public IReadOnlyList<string> GetRouteKeys()
    {
        return _routes.Keys.ToList().AsReadOnly();
    }
}
