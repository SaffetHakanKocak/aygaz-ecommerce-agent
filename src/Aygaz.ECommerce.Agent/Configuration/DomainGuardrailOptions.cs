namespace Aygaz.ECommerce.Agent.Configuration;

public sealed class DomainGuardrailOptions
{
    public const string SectionName = "DomainGuardrail";

    public string Domain { get; init; } = string.Empty;

    public string[] AllowedOrganizations { get; init; } = [];

    public string[] AllowedCapabilities { get; init; } = [];

    public int MaxInputCharacters { get; init; } = 1000;

    public int ClassifierMaxOutputTokens { get; init; } = 32;
}
