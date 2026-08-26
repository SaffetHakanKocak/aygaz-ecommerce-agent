#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public static class SupportPolicyAgentRegistration
{
    public const string RouteKey = "policy";
    public const string AgentName = "SupportPolicyAgent";

    public const string Instructions =
        "Only handle Aygaz support policy, return policy, delivery policy, campaign rule, and procedure questions. " +
        "Reply in Turkish using plain text only. Search support policy documents before answering. " +
        "Ground answers in retrieved document text and say when the answer is not found. " +
        "Never invent policy details. Do not expose internal function names.";

    public static ChatCompletionAgent Register(
        IAgentRegistrar registrar,
        IDocumentRetrievalService documentRetrievalService)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(documentRetrievalService);

        ChatCompletionAgent agent = registrar.Register(new AgentRegistration
        {
            RouteKey = RouteKey,
            Definition = new AgentDefinition
            {
                Name = AgentName,
                Instructions = Instructions
            },
            Plugins = [new SupportPolicyPlugin(documentRetrievalService)]
        });

        agent.Kernel.AutoFunctionInvocationFilters.Add(new OperationResultTerminationFilter());
        return agent;
    }
}
