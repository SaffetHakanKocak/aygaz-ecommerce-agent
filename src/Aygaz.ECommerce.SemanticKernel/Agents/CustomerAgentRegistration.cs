#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public static class CustomerAgentRegistration
{
    public const string RouteKey = "customer";
    public const string AgentName = "CustomerAgent";

    public const string Instructions =
        "Only handle customer lookup requests. Reply in Turkish. " +
        "Use an available function when a specific email, numeric customer id/customer number, or person name is provided. " +
        "Never invent customer data. " +
        "Do not perform bulk customer listing. " +
        "Do not expose internal function names. " +
        "If the request does not identify a specific customer, ask for identifying information or explain that bulk listing is unsupported.";

    public static ChatCompletionAgent Register(
        IAgentRegistrar registrar,
        ICustomerService customerService,
        int maxNameSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(customerService);

        ChatCompletionAgent agent = registrar.Register(new AgentRegistration
        {
            RouteKey = RouteKey,
            Definition = new AgentDefinition
            {
                Name = AgentName,
                Instructions = Instructions
            },
            Plugins = [new CustomerPlugin(customerService, maxNameSearchResults)]
        });

        agent.Kernel.AutoFunctionInvocationFilters.Add(new CustomerExactLookupTerminationFilter());
        return agent;
    }
}
