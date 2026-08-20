#pragma warning disable SKEXP0110

using System.Diagnostics;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.AgentFramework.Execution;

public interface IAgentRunner
{
    Task<AgentResponse> InvokeAsync(ChatCompletionAgent agent, string userMessage, CancellationToken cancellationToken = default);
}

public sealed record AgentResponse(
    string Content,
    TimeSpan Duration,
    int? InputTokens = null,
    int? OutputTokens = null);

public sealed class SemanticKernelAgentRunner : IAgentRunner
{
    public async Task<AgentResponse> InvokeAsync(ChatCompletionAgent agent, string userMessage, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var message = new ChatMessageContent(AuthorRole.User, userMessage);
        string result = string.Empty;
        int? inputTokens = null;
        int? outputTokens = null;

        await foreach (ChatMessageContent response in agent.InvokeAsync(message, cancellationToken: cancellationToken))
        {
            AccumulateUsage(response, ref inputTokens, ref outputTokens);

            if (!string.IsNullOrEmpty(response.Content))
            {
                result = response.Content;
                continue;
            }

            foreach (KernelContent item in response.Items)
            {
                if (item is FunctionResultContent functionResult
                    && functionResult.Result is string formatted
                    && !string.IsNullOrWhiteSpace(formatted))
                {
                    result = formatted;
                }
            }
        }

        sw.Stop();
        return new AgentResponse(
            result.Length > 0 ? result : "(boş yanıt)",
            sw.Elapsed,
            inputTokens,
            outputTokens);
    }

    private static void AccumulateUsage(ChatMessageContent response, ref int? inputTokens, ref int? outputTokens)
    {
        if (response.Metadata is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object?> entry in response.Metadata)
        {
            if (!string.Equals(entry.Key, "Usage", StringComparison.OrdinalIgnoreCase)
                || entry.Value is null)
            {
                continue;
            }

            AddIfInt(entry.Value, "InputTokenCount", ref inputTokens);
            AddIfInt(entry.Value, "OutputTokenCount", ref outputTokens);
        }
    }

    private static void AddIfInt(object usage, string propertyName, ref int? total)
    {
        object? value = usage.GetType().GetProperty(propertyName)?.GetValue(usage);
        if (value is int count)
        {
            total = (total ?? 0) + count;
        }
    }
}
