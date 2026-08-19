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

public sealed record AgentResponse(string Content, TimeSpan Duration);

public sealed class SemanticKernelAgentRunner : IAgentRunner
{
    public async Task<AgentResponse> InvokeAsync(ChatCompletionAgent agent, string userMessage, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var message = new ChatMessageContent(AuthorRole.User, userMessage);
        string result = string.Empty;

        await foreach (ChatMessageContent response in agent.InvokeAsync(message, cancellationToken: cancellationToken))
        {
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
        return new AgentResponse(result.Length > 0 ? result : "(boş yanıt)", sw.Elapsed);
    }
}
