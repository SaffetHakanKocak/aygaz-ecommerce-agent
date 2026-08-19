namespace Aygaz.ECommerce.Agent.Services;

public sealed class NullOllamaPerformanceLogger : IOllamaPerformanceLogger
{
    public static NullOllamaPerformanceLogger Instance { get; } = new();

    private NullOllamaPerformanceLogger()
    {
    }

    public void LogCall(OllamaCallMetrics metrics)
    {
    }

    public void LogRequestSummary(int callCount, long elapsedMilliseconds, bool fastPathUsed)
    {
    }
}
