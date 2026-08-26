#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public static class ProductAgentRegistration
{
    public const string RouteKey = "product";
    public const string AgentName = "ProductAgent";

    public const string Instructions =
        "Only handle Aygaz product lookup and product search requests. Reply in Turkish using plain text only. " +
        "Use get_product_by_sku for exact SKU values such as AYG-DEMO-PRD-001. " +
        "Use search_products for product name, category, or partial SKU searches. " +
        "Do not answer inventory quantities; if the user asks stock availability, ask them to use a stock query. " +
        "Never invent product data. Do not expose internal function names.";

    public static ChatCompletionAgent Register(
        IAgentRegistrar registrar,
        IProductService productService,
        int maxProductSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(productService);

        ChatCompletionAgent agent = registrar.Register(new AgentRegistration
        {
            RouteKey = RouteKey,
            Definition = new AgentDefinition
            {
                Name = AgentName,
                Instructions = Instructions
            },
            Plugins = [new ProductPlugin(productService, maxProductSearchResults)]
        });

        agent.Kernel.AutoFunctionInvocationFilters.Add(new OperationResultTerminationFilter());
        return agent;
    }
}
