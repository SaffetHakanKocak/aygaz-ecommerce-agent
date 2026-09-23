namespace Aygaz.AgentFramework.Observability;

public sealed class KernelInvocationTelemetry
{
    public int FunctionInvocationCount { get; set; }

    public string? LastFunctionName { get; set; }

    public int AutoFunctionRequestSequenceIndex { get; set; }

    public bool Terminated { get; set; }

    public void Reset()
    {
        FunctionInvocationCount = 0;
        LastFunctionName = null;
        AutoFunctionRequestSequenceIndex = 0;
        Terminated = false;
    }

    public int EstimateLlmInferenceCount()
    {
        if (FunctionInvocationCount == 0)
        {
            return 1;
        }

        int calls = AutoFunctionRequestSequenceIndex + 1;
        if (!Terminated)
        {
            calls++;
        }

        return calls;
    }
}
