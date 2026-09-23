namespace Aygaz.AgentFramework.Routing;

public interface IAgentRouter
{
    void MapRoute(string routeKey, string agentName);

    string? ResolveAgentName(string routeKey);

    IReadOnlyList<string> GetRouteKeys();
}
