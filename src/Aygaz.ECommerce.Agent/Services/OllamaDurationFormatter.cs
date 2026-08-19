namespace Aygaz.ECommerce.Agent.Services;

public static class OllamaDurationFormatter
{
    public static string FormatNanoseconds(long? nanoseconds)
    {
        if (nanoseconds is null or <= 0)
        {
            return "n/a";
        }

        double milliseconds = nanoseconds.Value / 1_000_000d;

        return milliseconds < 1_000d
            ? $"{milliseconds:F1}ms"
            : $"{milliseconds / 1_000d:F2}s";
    }
}
