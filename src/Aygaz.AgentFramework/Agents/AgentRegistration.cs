namespace Aygaz.AgentFramework.Agents;

public sealed class AgentRegistration
{
    public required string RouteKey { get; init; }

    public required AgentDefinition Definition { get; init; }

    public required IReadOnlyList<object> Plugins { get; init; }
}
