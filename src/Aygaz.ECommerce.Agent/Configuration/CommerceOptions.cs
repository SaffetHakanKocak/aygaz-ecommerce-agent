namespace Aygaz.ECommerce.Agent.Configuration;

public sealed class CommerceOptions
{
    public const string SectionName = "Commerce";

    public string CurrencyCode { get; init; } = "TRY";

    public string AnalyticsReferenceDate { get; init; } = "2026-03-06";

    public int MaximumAnalysisRangeDays { get; init; } = 366;

    public int MaximumTopProducts { get; init; } = 10;
}
