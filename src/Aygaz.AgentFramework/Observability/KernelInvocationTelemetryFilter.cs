#pragma warning disable SKEXP0001

using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Observability;

public sealed class KernelInvocationTelemetryFilter : IFunctionInvocationFilter, IAutoFunctionInvocationFilter
{
    private readonly KernelInvocationTelemetry _telemetry;

    public KernelInvocationTelemetryFilter(KernelInvocationTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _telemetry = telemetry;
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        _telemetry.FunctionInvocationCount++;
        _telemetry.LastFunctionName = context.Function.Name;
        await next(context);
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        await next(context);
        _telemetry.AutoFunctionRequestSequenceIndex = context.RequestSequenceIndex;
        _telemetry.Terminated = context.Terminate;
    }
}
