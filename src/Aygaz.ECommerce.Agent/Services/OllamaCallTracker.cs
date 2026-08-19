namespace Aygaz.ECommerce.Agent.Services;

public sealed class OllamaCallTracker : IOllamaCallTracker
{
    private static readonly AsyncLocal<int> CurrentCallCount = new();

    public int CallCount => CurrentCallCount.Value;

    public void Reset()
    {
        CurrentCallCount.Value = 0;
    }

    public void RecordCall()
    {
        CurrentCallCount.Value++;
    }
}
