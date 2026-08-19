namespace Aygaz.ECommerce.Agent.Services;

public sealed class OllamaCallTracker : IOllamaCallTracker
{
    private readonly List<OllamaCallMetrics> _calls = [];
    private bool _fastPathUsed;

    public int CallCount => _calls.Count;

    public bool FastPathUsed => _fastPathUsed;

    public IReadOnlyList<OllamaCallMetrics> Calls => _calls;

    public void Reset()
    {
        _calls.Clear();
        _fastPathUsed = false;
    }

    public void RecordCall(OllamaCallMetrics metrics)
    {
        _calls.Add(metrics);
    }

    public void MarkFastPathUsed()
    {
        _fastPathUsed = true;
    }
}
