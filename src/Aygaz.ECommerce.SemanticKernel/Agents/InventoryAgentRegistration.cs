#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public static class InventoryAgentRegistration
{
    public const string RouteKey = "inventory";
    public const string AgentName = "InventoryAgent";

    public const string Instructions =
        "Only handle Aygaz inventory and stock availability requests. Reply in Turkish using plain text only. " +
        "If the user gives a SKU or product name, first use product lookup/search to find one clear product id, then use inventory functions. " +
        "If product lookup returns multiple products, ask the user to clarify. " +
        "Report total stock or location-level stock depending on the question. Never invent inventory data. " +
        "Do not expose internal function names.";

    public static ChatCompletionAgent Register(
        IAgentRegistrar registrar,
        IProductService productService,
        IInventoryService inventoryService,
        int maxProductSearchResults = 5,
        int maxInventoryLocationResults = 5)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(productService);
        ArgumentNullException.ThrowIfNull(inventoryService);

        ChatCompletionAgent agent = registrar.Register(new AgentRegistration
        {
            RouteKey = RouteKey,
            Definition = new AgentDefinition
            {
                Name = AgentName,
                Instructions = Instructions
            },
            Plugins =
            [
                new ProductPlugin(productService, maxProductSearchResults),
                new InventoryPlugin(inventoryService, maxInventoryLocationResults)
            ]
        });

        agent.Kernel.AutoFunctionInvocationFilters.Add(new OperationResultTerminationFilter());
        return agent;
    }
}
