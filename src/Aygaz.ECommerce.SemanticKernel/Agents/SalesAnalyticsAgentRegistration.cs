#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public static class SalesAnalyticsAgentRegistration
{
    public const string RouteKey = "sales";
    public const string AgentName = "SalesAnalyticsAgent";

    public const string Instructions =
        "Only handle Aygaz sales analytics requests. Reply in Turkish using plain text only. " +
        "Use ISO dates in yyyy-MM-dd format for all analytics functions. " +
        "For relative dates, use the configured demo reference date from the function descriptions. " +
        "Return aggregate metrics only; never list raw order rows. Never invent sales figures. " +
        "Do not expose internal function names.";

    public static ChatCompletionAgent Register(
        IAgentRegistrar registrar,
        ISalesAnalyticsService salesAnalyticsService,
        CommerceOptions commerceOptions)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(salesAnalyticsService);
        ArgumentNullException.ThrowIfNull(commerceOptions);

        ChatCompletionAgent agent = registrar.Register(new AgentRegistration
        {
            RouteKey = RouteKey,
            Definition = new AgentDefinition
            {
                Name = AgentName,
                Instructions = Instructions
            },
            Plugins = [new SalesAnalyticsPlugin(salesAnalyticsService, commerceOptions)]
        });

        agent.Kernel.AutoFunctionInvocationFilters.Add(new OperationResultTerminationFilter());
        return agent;
    }
}
