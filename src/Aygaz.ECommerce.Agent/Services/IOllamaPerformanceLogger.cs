namespace Aygaz.ECommerce.Agent.Services;

public interface IOllamaPerformanceLogger
{
    void LogCall(OllamaCallMetrics metrics);

    void LogRequestSummary(int callCount, long elapsedMilliseconds, bool fastPathUsed);
}
