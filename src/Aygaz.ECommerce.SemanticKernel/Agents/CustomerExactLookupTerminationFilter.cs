#pragma warning disable SKEXP0001

using Aygaz.ECommerce.SemanticKernel.Formatting;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public sealed class CustomerExactLookupTerminationFilter : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        await next(context);

        object? value = context.Result?.GetValue<object>();
        if (!CustomerLookupResponseFormatter.TryFormatExactLookup(context.Function.Name, value, out string text))
        {
            return;
        }

        context.Result = new FunctionResult(context.Function, text);
        context.Terminate = true;
    }
}
