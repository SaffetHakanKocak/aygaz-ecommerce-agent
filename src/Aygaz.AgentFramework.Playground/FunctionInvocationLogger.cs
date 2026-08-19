using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Playground;

internal sealed class FunctionInvocationLogger : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        Console.WriteLine($"  KernelFunction invoked: {context.Function.Name}");
        await next(context);
    }
}
