namespace Aygaz.ECommerce.Agent.Services;

public interface IOllamaCallTracker
{
    int CallCount { get; }

    bool FastPathUsed { get; }

    IReadOnlyList<OllamaCallMetrics> Calls { get; }

    void Reset();

    void RecordCall(OllamaCallMetrics metrics);

    void MarkFastPathUsed();
}
