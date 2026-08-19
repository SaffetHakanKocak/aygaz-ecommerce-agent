namespace Aygaz.ECommerce.Agent.Services;

public interface IOllamaCallTracker
{
    int CallCount { get; }

    void Reset();

    void RecordCall();
}
