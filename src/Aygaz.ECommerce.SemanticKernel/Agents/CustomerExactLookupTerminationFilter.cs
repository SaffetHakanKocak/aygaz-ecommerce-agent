#pragma warning disable SKEXP0001

using Aygaz.ECommerce.SemanticKernel.Formatting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public sealed class CustomerExactLookupTerminationFilter : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        await next(context);

        object? value = context.Result?.GetValue<object>();
        string? userMessage = GetLastUserMessage(context.ChatHistory);
        if (!CustomerLookupResponseFormatter.TryFormatExactLookup(
            context.Function.Name,
            value,
            userMessage,
            out string text))
        {
            return;
        }

        context.Result = new FunctionResult(context.Function, text);
        context.Terminate = true;
    }

    private static string? GetLastUserMessage(ChatHistory? history)
    {
        if (history is null)
        {
            return null;
        }

        for (int index = history.Count - 1; index >= 0; index--)
        {
            ChatMessageContent message = history[index];
            if (message.Role == AuthorRole.User && !string.IsNullOrWhiteSpace(message.Content))
            {
                return message.Content;
            }
        }

        return null;
    }
}
