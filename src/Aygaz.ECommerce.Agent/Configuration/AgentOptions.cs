namespace Aygaz.ECommerce.Agent.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public int MaxToolIterations { get; init; } = 5;

    public int MaxToolCallsPerIteration { get; init; } = 3;

    public int MaxNameSearchResults { get; init; } = 5;

    public int MaxOrderSearchResults { get; init; } = 5;

    public int MaxProductSearchResults { get; init; } = 5;

    public int MaxInventoryLocationResults { get; init; } = 5;

    public int MaxConversationTurns { get; init; } = 4;
}
