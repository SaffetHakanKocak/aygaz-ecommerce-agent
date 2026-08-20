namespace Aygaz.ECommerce.SemanticKernel.Web.Configuration;

public sealed class ChatApiOptions
{
    public const string SectionName = "ChatApi";

    public int MaxMessageLength { get; init; } = 2000;
}
